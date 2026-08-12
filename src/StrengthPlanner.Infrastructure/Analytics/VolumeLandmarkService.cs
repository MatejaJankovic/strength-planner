using Microsoft.EntityFrameworkCore;
using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Domain.Enums;
using StrengthPlanner.Infrastructure.Persistence;

namespace StrengthPlanner.Infrastructure.Analytics;

/// <summary>
/// Čita lične MEV/MAV/MRV granice i pomera ih posle svake završene nedelje.
/// Seed vrednosti iz <see cref="VolumeLandmark"/> ostaju referentna tačka: lične
/// granice su dozvoljene samo u pojasu oko njih i tu se vraćaju na reset.
/// </summary>
public sealed class VolumeLandmarkService
{
    private readonly AppDbContext _db;

    public VolumeLandmarkService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Efektivne granice po mišićnoj grupi: lične ako postoje, inače seed vrednosti.
    /// </summary>
    public async Task<Dictionary<Guid, EffectiveLandmark>> GetEffectiveAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var seeds = await GetScaledSeedsAsync(userId, cancellationToken);

        var personal = await _db.UserVolumeLandmarks
            .AsNoTracking()
            .Where(landmark => landmark.UserId == userId)
            .ToDictionaryAsync(landmark => landmark.MuscleGroupId, cancellationToken);

        return seeds.ToDictionary(
            entry => entry.Key,
            entry =>
            {
                var seed = entry.Value;

                if (personal.TryGetValue(entry.Key, out var own))
                {
                    return new EffectiveLandmark(
                        own.Mev,
                        own.Mav,
                        own.Mrv,
                        seed.Mev,
                        seed.Mav,
                        seed.Mrv,
                        IsPersonal: own.Mev != seed.Mev || own.Mav != seed.Mav || own.Mrv != seed.Mrv,
                        own.UpdatedAt);
                }

                return new EffectiveLandmark(
                    seed.Mev,
                    seed.Mav,
                    seed.Mrv,
                    seed.Mev,
                    seed.Mav,
                    seed.Mrv,
                    IsPersonal: false,
                    null);
            });
    }

    /// <summary>
    /// Pomera granice na osnovu svake nedelje mezociklusa koja je u celosti odrađena a
    /// još nije obračunata. Radi se nad celim mezociklusom, a ne samo nad nedeljom kojoj
    /// pripada upravo završena sesija, da nedelja ne bi trajno propala ako dva zahteva
    /// istovremeno završe njene poslednje dve sesije i nijedan je ne vidi kao gotovu.
    /// Deload nedelje se preskaču: namerno su submaksimalne, pa o toleranciji ne govore.
    /// </summary>
    public async Task AdaptPendingWeeksAsync(
        Guid userId,
        Guid mesocycleId,
        DateTime completedAt,
        CancellationToken cancellationToken)
    {
        var pendingWeekIds = await _db.TrainingWeeks
            .AsNoTracking()
            .Where(week => week.MesocycleId == mesocycleId
                           && week.Mesocycle.UserId == userId
                           && !week.IsDeload
                           && week.VolumeAdaptedAt == null
                           && week.Sessions.All(session => session.Status == SessionStatus.Completed))
            .OrderBy(week => week.WeekNumber)
            .Select(week => week.Id)
            .ToListAsync(cancellationToken);

        foreach (var weekId in pendingWeekIds)
        {
            await AdaptWeekAsync(userId, weekId, completedAt, cancellationToken);
        }
    }

    private async Task AdaptWeekAsync(
        Guid userId,
        Guid trainingWeekId,
        DateTime completedAt,
        CancellationToken cancellationToken)
    {
        // Uslovni UPDATE je i zaključavanje i provera: drugi zahtev koji je istovremeno
        // stigao dovde čeka na redu, pa vidi već upisan datum i dobija nula redova.
        var claimed = await _db.TrainingWeeks
            .Where(week => week.Id == trainingWeekId
                           && week.Mesocycle.UserId == userId
                           && week.VolumeAdaptedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(week => week.VolumeAdaptedAt, completedAt),
                cancellationToken);

        if (claimed == 0)
        {
            return;
        }

        var responses = await GetWeeklyResponsesAsync(userId, trainingWeekId, cancellationToken);
        if (responses.Count == 0)
        {
            return;
        }

        var seeds = await GetScaledSeedsAsync(userId, cancellationToken);

        var personal = await _db.UserVolumeLandmarks
            .Where(landmark => landmark.UserId == userId)
            .ToDictionaryAsync(landmark => landmark.MuscleGroupId, cancellationToken);

        foreach (var (muscleGroupId, response) in responses)
        {
            if (!seeds.TryGetValue(muscleGroupId, out var seed))
            {
                continue;
            }

            personal.TryGetValue(muscleGroupId, out var row);
            var current = row is null
                ? seed
                : new VolumeLandmarkValues(row.Mev, row.Mav, row.Mrv);

            var adjusted = VolumeAdaptation.Adjust(current, seed, response);
            if (adjusted == current)
            {
                continue;
            }

            if (row is null)
            {
                _db.UserVolumeLandmarks.Add(new UserVolumeLandmark
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    MuscleGroupId = muscleGroupId,
                    Mev = adjusted.Mev,
                    Mav = adjusted.Mav,
                    Mrv = adjusted.Mrv,
                    UpdatedAt = completedAt
                });
            }
            else
            {
                row.Mev = adjusted.Mev;
                row.Mav = adjusted.Mav;
                row.Mrv = adjusted.Mrv;
                row.UpdatedAt = completedAt;
            }
        }
    }

    /// <summary>
    /// Populacione granice skalirane nivoom iskustva korisnika.
    ///
    /// Serija početnika je slabiji stimulus i njegov oporavak je nerazvijen, pa mu ceo
    /// pojas stoji niže; napredan vežbač podnosi i traži više. Adaptacija onda kreće od
    /// te tačke, a ne od populacionog proseka — i reset se vraća na nju.
    /// </summary>
    private async Task<Dictionary<Guid, VolumeLandmarkValues>> GetScaledSeedsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var level = await _db.Profiles
            .AsNoTracking()
            .Where(profile => profile.UserId == userId)
            .Select(profile => (ExperienceLevel?)profile.ExperienceLevel)
            .FirstOrDefaultAsync(cancellationToken) ?? ExperienceLevel.Intermediate;

        var seeds = await _db.VolumeLandmarks
            .AsNoTracking()
            .Select(landmark => new { landmark.MuscleGroupId, landmark.Mev, landmark.Mav, landmark.Mrv })
            .ToListAsync(cancellationToken);

        return seeds.ToDictionary(
            seed => seed.MuscleGroupId,
            seed => ExperienceProgramming.ScaleLandmarks(
                new VolumeLandmarkValues(seed.Mev, seed.Mav, seed.Mrv),
                level));
    }

    /// <summary>
    /// Vraća lične granice na seed vrednosti (briše redove). Promene ostaju u change
    /// trackeru — pozivalac ih upisuje.
    /// </summary>
    public async Task ResetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var rows = await _db.UserVolumeLandmarks
            .Where(landmark => landmark.UserId == userId)
            .ToListAsync(cancellationToken);

        _db.UserVolumeLandmarks.RemoveRange(rows);
    }

    /// <summary>
    /// Sažima nedelju u jedan odgovor po mišićnoj grupi. Sve se meri doprinosom serije
    /// (primarna 1.0, sekundarna 0.5) pomnoženim stimulativnim udelom, pa ni sekundarni
    /// rad ni serija daleko od otkaza ne broje kao pun.
    /// </summary>
    public async Task<Dictionary<Guid, VolumeResponse>> GetWeeklyResponsesAsync(
        Guid userId,
        Guid trainingWeekId,
        CancellationToken cancellationToken)
    {
        var contributions = await _db.SetLogs
            .AsNoTracking()
            .Where(set => set.ExercisePlan.WorkoutSession.TrainingWeekId == trainingWeekId
                          && set.ExercisePlan.WorkoutSession.TrainingWeek.Mesocycle.UserId == userId)
            .SelectMany(set => set.ExercisePlan.Exercise.Muscles.Select(muscle => new SetContribution(
                muscle.MuscleGroupId,
                muscle.Contribution,
                set.Reps,
                set.Rir,
                set.IsFailure,
                set.ExercisePlan.TargetRir,
                set.ExercisePlan.RepRangeMin)))
            .ToListAsync(cancellationToken);

        return contributions
            .GroupBy(item => item.MuscleGroupId)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var measured = group
                        .Select(item => new
                        {
                            item.Contribution,
                            item.IsFailure,
                            Set = new WorkingSet(item.Reps, item.Rir, item.IsFailure),
                            item.TargetRir,
                            item.RepRangeMin
                        })
                        .Select(item => new
                        {
                            item.Contribution,
                            item.IsFailure,
                            item.Set,
                            Credit = StimulativeVolume.CreditFor(item.Set),
                            item.TargetRir,
                            item.RepRangeMin
                        })
                        .ToList();

                    // Zamor pravi svaka odrađena serija, pa se on meri sirovim zbirom.
                    var rawWeight = measured.Sum(item => item.Contribution);

                    // Stimulus pravi samo serija blizu otkaza — to je mera koju granice uče.
                    var stimulativeWeight = measured.Sum(item => item.Contribution * item.Credit);

                    // Udeo otkaza je pitanje o zamoru, pa deli sirovim zbirom. Sa
                    // stimulativnim imeniocem bi nedelja od deset laganih i dve otkazane
                    // serije prijavila udeo 1.0 umesto 0.17, pa bi pragovi umora
                    // (0.25 / 0.125) izgubili kalibraciju i MEV bi se survao na svoj pod.
                    var failed = measured
                        .Where(item => item.IsFailure)
                        .Sum(item => item.Contribution);
                    var failureShare = rawWeight == 0 ? 0m : failed / rawWeight;

                    if (stimulativeWeight == 0)
                    {
                        // Nedelja bez ijedne stimulativne serije ne govori ništa o tome
                        // koliko volumena korisnik podnosi. Odstupanje RIR-a se namerno
                        // ne prijavljuje: sve te serije nose veliko pozitivno odstupanje
                        // ("prelako"), ali to je posao progresije da ispravi opterećenjem,
                        // a ne granica volumena da protumači kao toleranciju.
                        return new VolumeResponse(0, rawWeight, 0, failureShare);
                    }

                    // Prosek mora da deli isti ponder sa imeniocem: kada se volumen meri
                    // stimulativnim doprinosom, i odstupanje RIR-a se meri nad istim tim
                    // serijama, inače to nije ponderisani prosek nego mešavina dve mere.
                    var deviation = measured.Sum(item =>
                        item.Contribution
                        * item.Credit
                        * (item.Set.EffectiveRir(item.RepRangeMin) - item.TargetRir));

                    return new VolumeResponse(
                        stimulativeWeight,
                        rawWeight,
                        deviation / stimulativeWeight,
                        failureShare);
                });
    }

    private sealed record SetContribution(
        Guid MuscleGroupId,
        decimal Contribution,
        int Reps,
        int Rir,
        bool IsFailure,
        int TargetRir,
        int RepRangeMin);
}

/// <summary>Granice koje važe za korisnika, uz seed vrednosti radi poređenja u UI-ju.</summary>
public sealed record EffectiveLandmark(
    int Mev,
    int Mav,
    int Mrv,
    int SeedMev,
    int SeedMav,
    int SeedMrv,
    bool IsPersonal,
    DateTime? UpdatedAt);
