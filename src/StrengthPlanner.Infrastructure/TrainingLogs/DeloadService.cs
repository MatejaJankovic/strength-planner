using Microsoft.EntityFrameworkCore;
using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Enums;
using StrengthPlanner.Infrastructure.Analytics;
using StrengthPlanner.Infrastructure.Exercises;
using StrengthPlanner.Infrastructure.Persistence;

namespace StrengthPlanner.Infrastructure.TrainingLogs;

/// <summary>
/// Ocenjuje umor iz završene nedelje i, ako je prešao prag, pretvara sledeću nedelju u
/// deload. Planirani deload u četvrtoj nedelji ostaje kao donja granica — ovo ga samo
/// može povući ranije kada podaci to traže.
/// </summary>
public sealed class DeloadService
{
    private readonly AppDbContext _db;
    private readonly VolumeLandmarkService _landmarks;
    private readonly E1RmCalculator _e1RmCalculator = new();

    public DeloadService(AppDbContext db, VolumeLandmarkService landmarks)
    {
        _db = db;
        _landmarks = landmarks;
    }

    /// <summary>
    /// Ocenjuje svaku završenu nedelju mezociklusa koja još nema ocenu i po potrebi
    /// pretvara sledeću u deload. Ocena se upisuje uslovnim UPDATE-om, pa se nedelja
    /// ne može oceniti dvaput ni kada dva zahteva istovremeno završe njene sesije.
    /// </summary>
    public async Task<DeloadOutcome?> EvaluatePendingWeeksAsync(
        Guid userId,
        Guid mesocycleId,
        CancellationToken cancellationToken)
    {
        var pendingWeeks = await _db.TrainingWeeks
            .AsNoTracking()
            .Where(week => week.MesocycleId == mesocycleId
                           && week.Mesocycle.UserId == userId
                           && !week.IsDeload
                           && week.FatigueScore == null
                           && week.Sessions.All(session => session.Status == SessionStatus.Completed))
            .OrderBy(week => week.WeekNumber)
            .Select(week => new { week.Id, week.WeekNumber })
            .ToListAsync(cancellationToken);

        DeloadOutcome? outcome = null;

        foreach (var week in pendingWeeks)
        {
            var evaluated = await EvaluateWeekAsync(
                userId,
                mesocycleId,
                week.Id,
                week.WeekNumber,
                cancellationToken);

            outcome ??= evaluated;
        }

        return outcome;
    }

    private async Task<DeloadOutcome?> EvaluateWeekAsync(
        Guid userId,
        Guid mesocycleId,
        Guid weekId,
        int weekNumber,
        CancellationToken cancellationToken)
    {
        var fatigue = await BuildWeeklyFatigueAsync(userId, mesocycleId, weekId, weekNumber, cancellationToken);
        if (fatigue is null)
        {
            return null;
        }

        var score = FatigueEvaluator.Score(fatigue);

        // Upis ocene je ujedno i preuzimanje nedelje: drugi zahtev vidi ocenu i odustaje.
        var claimed = await _db.TrainingWeeks
            .Where(week => week.Id == weekId
                           && week.Mesocycle.UserId == userId
                           && week.FatigueScore == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(week => week.FatigueScore, score),
                cancellationToken);

        if (claimed == 0 || score < FatigueEvaluator.DeloadThreshold)
        {
            return null;
        }

        var nextWeek = await _db.TrainingWeeks
            .Where(week => week.MesocycleId == mesocycleId
                           && week.WeekNumber == weekNumber + 1
                           && !week.IsDeload)
            .FirstOrDefaultAsync(cancellationToken);

        // Nema sledeće nedelje ili je već deload — ocena je upisana, ali nema šta da se menja.
        if (nextWeek is null)
        {
            return null;
        }

        await ApplyDeloadAsync(userId, weekId, nextWeek.Id, cancellationToken);

        nextWeek.IsDeload = true;
        nextWeek.IsAutoDeload = true;

        return new DeloadOutcome(weekNumber, nextWeek.WeekNumber, score);
    }

    /// <summary>
    /// Pretvara nedelju u deload: prepolovljene serije i 90% opterećenja koje je STVARNO
    /// korišćeno u prethodnoj nedelji. Opterećenja se preračunavaju jer ih je progresija
    /// već popunila dok se prethodna nedelja završavala.
    /// </summary>
    private async Task ApplyDeloadAsync(
        Guid userId,
        Guid completedWeekId,
        Guid deloadWeekId,
        CancellationToken cancellationToken)
    {
        var usedWeights = await _db.SetLogs
            .AsNoTracking()
            .Where(set => set.ExercisePlan.WorkoutSession.TrainingWeekId == completedWeekId
                          && set.ExercisePlan.WorkoutSession.TrainingWeek.Mesocycle.UserId == userId)
            .GroupBy(set => new
            {
                set.ExercisePlan.ExerciseId,
                set.ExercisePlan.WorkoutSession.DayLabel
            })
            .Select(group => new
            {
                group.Key.ExerciseId,
                group.Key.DayLabel,
                AverageWeightKg = group.Average(set => set.WeightKg)
            })
            .ToListAsync(cancellationToken);

        var usedByExerciseAndDay = usedWeights.ToDictionary(
            item => (item.ExerciseId, item.DayLabel),
            item => item.AverageWeightKg);

        var plans = await _db.ExercisePlans
            .Include(plan => plan.WorkoutSession)
            .Where(plan => plan.WorkoutSession.TrainingWeekId == deloadWeekId)
            .ToListAsync(cancellationToken);

        var exerciseIds = plans.Select(plan => plan.ExerciseId).Distinct().ToList();
        var weightStepByExerciseId = await WeightStepResolver.ResolveAsync(
            _db,
            userId,
            exerciseIds,
            cancellationToken);

        foreach (var plan in plans)
        {
            plan.TargetSets = Math.Max(1, (int)Math.Ceiling(plan.TargetSets / 2m));

            var baseWeight = usedByExerciseAndDay.TryGetValue(
                (plan.ExerciseId, plan.WorkoutSession.DayLabel),
                out var used)
                ? used
                : plan.TargetWeightKg;

            if (baseWeight is null)
            {
                continue;
            }

            plan.TargetWeightKg = WeightMath.RoundToStep(
                baseWeight.Value * TrainingConstants.DeloadWeightFactor,
                WeightStepResolver.StepFor(weightStepByExerciseId, plan.ExerciseId));
        }
    }

    /// <summary>
    /// Skuplja četiri signala umora iz nedelje. Vraća null kada nedelja nema nijednu
    /// odrađenu seriju — o umoru se tada nema šta zaključiti.
    /// </summary>
    private async Task<WeeklyFatigue?> BuildWeeklyFatigueAsync(
        Guid userId,
        Guid mesocycleId,
        Guid weekId,
        int weekNumber,
        CancellationToken cancellationToken)
    {
        var sets = await _db.SetLogs
            .AsNoTracking()
            .Where(set => set.ExercisePlan.WorkoutSession.TrainingWeekId == weekId
                          && set.ExercisePlan.WorkoutSession.TrainingWeek.Mesocycle.UserId == userId)
            .Select(set => new SetSignal(
                set.ExercisePlan.ExerciseId,
                set.Reps,
                set.Rir,
                set.IsFailure,
                set.WeightKg,
                set.ExercisePlan.TargetRir,
                set.ExercisePlan.RepRangeMin))
            .ToListAsync(cancellationToken);

        if (sets.Count == 0)
        {
            return null;
        }

        var rirDeviation = sets.Average(set =>
            (decimal)(new WorkingSet(set.Reps, set.Rir, set.IsFailure).EffectiveRir(set.RepRangeMin)
                      - set.TargetRir));
        var failureShare = (decimal)sets.Count(set => set.IsFailure) / sets.Count;

        var e1RmChange = await GetE1RmChangeShareAsync(userId, mesocycleId, weekNumber, sets, cancellationToken);
        var volumeShare = await GetVolumeVsMrvShareAsync(userId, weekId, cancellationToken);

        return new WeeklyFatigue(rirDeviation, failureShare, e1RmChange, volumeShare);
    }

    /// <summary>
    /// Relativna promena najboljeg procenjenog 1RM u odnosu na prethodnu nedelju,
    /// usrednjena po vežbama koje su rađene u obe. Nula kada poređenja nema — nedostatak
    /// podatka ne sme da se protumači kao pad.
    /// </summary>
    private async Task<decimal> GetE1RmChangeShareAsync(
        Guid userId,
        Guid mesocycleId,
        int weekNumber,
        IReadOnlyList<SetSignal> currentSets,
        CancellationToken cancellationToken)
    {
        if (weekNumber <= 1)
        {
            return 0m;
        }

        var previousSets = await _db.SetLogs
            .AsNoTracking()
            .Where(set => set.ExercisePlan.WorkoutSession.TrainingWeek.MesocycleId == mesocycleId
                          && set.ExercisePlan.WorkoutSession.TrainingWeek.WeekNumber == weekNumber - 1
                          && set.ExercisePlan.WorkoutSession.TrainingWeek.Mesocycle.UserId == userId)
            .Select(set => new SetSignal(
                set.ExercisePlan.ExerciseId,
                set.Reps,
                set.Rir,
                set.IsFailure,
                set.WeightKg,
                set.ExercisePlan.TargetRir,
                set.ExercisePlan.RepRangeMin))
            .ToListAsync(cancellationToken);

        if (previousSets.Count == 0)
        {
            return 0m;
        }

        var current = BestE1RmByExercise(currentSets);
        var previous = BestE1RmByExercise(previousSets);

        var changes = current
            .Where(entry => previous.ContainsKey(entry.Key) && previous[entry.Key] > 0)
            .Select(entry => (entry.Value - previous[entry.Key]) / previous[entry.Key])
            .ToList();

        return changes.Count == 0 ? 0m : changes.Average();
    }

    private Dictionary<Guid, decimal> BestE1RmByExercise(IReadOnlyList<SetSignal> sets)
    {
        var best = new Dictionary<Guid, decimal>();

        foreach (var set in sets.Where(set => set.Reps <= TrainingConstants.EpleyRepCap))
        {
            var estimate = _e1RmCalculator.EstimateOneRepMax(set.WeightKg, set.Reps, set.Rir);

            if (!best.TryGetValue(set.ExerciseId, out var current) || estimate > current)
            {
                best[set.ExerciseId] = estimate;
            }
        }

        return best;
    }

    /// <summary>
    /// Najveći odnos odrađenog volumena i MRV-a među mišićnim grupama. Koristi lične
    /// granice ako ih korisnik ima, pa je i ovaj signal prilagođen njemu.
    /// </summary>
    private async Task<decimal> GetVolumeVsMrvShareAsync(
        Guid userId,
        Guid weekId,
        CancellationToken cancellationToken)
    {
        var responses = await _landmarks.GetWeeklyResponsesAsync(userId, weekId, cancellationToken);
        if (responses.Count == 0)
        {
            return 0m;
        }

        var landmarks = await _landmarks.GetEffectiveAsync(userId, cancellationToken);
        var shares = responses
            .Where(entry => landmarks.ContainsKey(entry.Key) && landmarks[entry.Key].Mrv > 0)
            .Select(entry => entry.Value.PerformedSets / landmarks[entry.Key].Mrv)
            .ToList();

        return shares.Count == 0 ? 0m : shares.Max();
    }

    private sealed record SetSignal(
        Guid ExerciseId,
        int Reps,
        int Rir,
        bool IsFailure,
        decimal WeightKg,
        int TargetRir,
        int RepRangeMin);
}

/// <summary>Rezultat automatskog deload-a, za poruku korisniku posle treninga.</summary>
public sealed record DeloadOutcome(int TriggeredByWeek, int DeloadWeek, decimal FatigueScore);
