using Microsoft.EntityFrameworkCore;
using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Enums;
using StrengthPlanner.Infrastructure.Analytics;
using StrengthPlanner.Infrastructure.Persistence;

namespace StrengthPlanner.Infrastructure.Mesocycles;

/// <summary>
/// Keeps the set proposals of a block pointed at the optimal volume zone.
///
/// The same call does both halves of the feature, which is what keeps them consistent:
/// when a block is generated every week is balanced from scratch, and when a session is
/// completed the weeks that still have untouched sessions are balanced again — this time
/// against what the lifter has actually banked. Because the allocator always starts from
/// the prescription rather than from its own previous answer, running it repeatedly
/// converges instead of drifting.
///
/// Only sessions that are still <see cref="SessionStatus.Planned"/> <b>and</b> hold no
/// logged sets are open to change. A session someone has already started logging into is
/// a session in progress, whatever its stored status says, and its proposal must not move
/// under their hands. It also keeps the arithmetic honest: its sets are counted once, as
/// volume already performed, and never a second time as volume still planned.
///
/// Deload weeks are skipped entirely. Their halved sets are the whole point of the week,
/// and pulling them back up to MAV would undo the rest.
/// </summary>
public sealed class WeeklySetPlanner
{
    private readonly AppDbContext _db;
    private readonly VolumeLandmarkService _landmarks;

    public WeeklySetPlanner(AppDbContext db, VolumeLandmarkService landmarks)
    {
        _db = db;
        _landmarks = landmarks;
    }

    /// <summary>
    /// Rebalances every week of the block that still has an untouched session. Changes stay
    /// in the change tracker — the caller decides when they are written.
    /// </summary>
    public async Task<IReadOnlyList<SetAdjustment>> RebalanceAsync(
        Guid userId,
        Guid mesocycleId,
        CancellationToken cancellationToken)
    {
        var weeks = await _db.TrainingWeeks
            .AsNoTracking()
            .Where(week => week.MesocycleId == mesocycleId
                           && week.Mesocycle.UserId == userId
                           && !week.IsDeload
                           && week.Sessions.Any(session =>
                               session.Status == SessionStatus.Planned
                               && !session.ExercisePlans.Any(plan => plan.SetLogs.Any())))
            .OrderBy(week => week.WeekNumber)
            .Select(week => new { week.Id, week.WeekNumber })
            .ToListAsync(cancellationToken);

        if (weeks.Count == 0)
        {
            return [];
        }

        var landmarks = await _landmarks.GetEffectiveAsync(userId, cancellationToken);
        var muscleNames = await _db.MuscleGroups
            .AsNoTracking()
            .ToDictionaryAsync(group => group.Id, group => group.Name, cancellationToken);

        var adjustments = new List<SetAdjustment>();

        foreach (var week in weeks)
        {
            adjustments.AddRange(await RebalanceWeekAsync(
                userId,
                week.Id,
                week.WeekNumber,
                landmarks,
                muscleNames,
                cancellationToken));
        }

        return adjustments;
    }

    private async Task<IReadOnlyList<SetAdjustment>> RebalanceWeekAsync(
        Guid userId,
        Guid trainingWeekId,
        int weekNumber,
        IReadOnlyDictionary<Guid, EffectiveLandmark> landmarks,
        IReadOnlyDictionary<Guid, string> muscleNames,
        CancellationToken cancellationToken)
    {
        // Praćeni upit namerno: pri generisanju bloka ovo vraća iste instance koje je
        // generator upravo napravio, pa balansiranje stigne i u odgovor koji korisnik
        // dobija, a ne tek u bazu.
        var plans = await _db.ExercisePlans
            .Include(plan => plan.WorkoutSession)
            .Include(plan => plan.Exercise)
            .Where(plan => plan.WorkoutSession.TrainingWeekId == trainingWeekId
                           && plan.WorkoutSession.TrainingWeek.Mesocycle.UserId == userId
                           && plan.WorkoutSession.Status == SessionStatus.Planned
                           && !plan.WorkoutSession.ExercisePlans.Any(other => other.SetLogs.Any()))
            .OrderBy(plan => plan.WorkoutSession.Date)
                .ThenBy(plan => plan.WorkoutSession.DayLabel)
                .ThenBy(plan => plan.Order)
            .ToListAsync(cancellationToken);

        if (plans.Count == 0)
        {
            return [];
        }

        var exerciseIds = plans.Select(plan => plan.ExerciseId).Distinct().ToList();
        var musclesByExerciseId = await LoadMuscleLoadsAsync(exerciseIds, cancellationToken);

        var slots = plans
            .Select(plan => new ExerciseSetSlot(
                plan.Id,
                plan.PrescribedSets,
                musclesByExerciseId.GetValueOrDefault(plan.ExerciseId, [])))
            .ToList();

        // Šta je nedelja već upisala. Dve mere, jer na dva pitanja odgovaraju: koliko
        // stimulusa još nedostaje do cilja, i koliko je oporavka već potrošeno.
        var responses = await _landmarks.GetWeeklyResponsesAsync(userId, trainingWeekId, cancellationToken);
        var completedStimulative = responses.ToDictionary(
            entry => entry.Key,
            entry => entry.Value.PerformedSets);
        var completedRaw = responses.ToDictionary(
            entry => entry.Key,
            entry => entry.Value.RawSets);

        var targets = slots
            .SelectMany(slot => slot.Muscles.Select(muscle => muscle.MuscleGroupId))
            .Concat(responses.Keys)
            .Distinct()
            .Where(landmarks.ContainsKey)
            .OrderBy(muscleGroupId => muscleGroupId)
            .Select(muscleGroupId => new MuscleVolumeTarget(
                muscleGroupId,
                landmarks[muscleGroupId].Mav,
                landmarks[muscleGroupId].Mrv))
            .ToList();

        var allocated = WeeklySetAllocation.Allocate(slots, targets, completedStimulative, completedRaw);

        // Objašnjenje se čita iz konačne raspodele, sa jednom vežbom vraćenom na propis —
        // dakle iz pitanja "šta bi ovom mišiću bilo da se ova vežba nije pomerila".
        //
        // Polazno stanje ovde ne služi: pritisak koji je vežbu pomerio često u njemu još
        // ne postoji. Zgib je porastao zbog leđa i time povukao biceps preko cilja, pa je
        // Hammer Curl morao dole — na početku bloka biceps je stajao tačno na cilju i to
        // pomeranje je ostajalo bez ijednog objašnjenja.
        var finalVolume = WeeklySetAllocation.Project(slots, allocated, completedStimulative);
        var targetByMuscleGroupId = targets.ToDictionary(target => target.MuscleGroupId);
        var slotById = slots.ToDictionary(slot => slot.Id);

        var adjustments = new List<SetAdjustment>();

        foreach (var plan in plans)
        {
            if (!allocated.TryGetValue(plan.Id, out var sets) || sets == plan.TargetSets)
            {
                continue;
            }

            var previousSets = plan.TargetSets;
            plan.TargetSets = sets;

            adjustments.Add(new SetAdjustment(
                plan.WorkoutSessionId,
                weekNumber,
                plan.WorkoutSession.DayLabel,
                plan.ExerciseId,
                plan.Exercise.Name,
                previousSets,
                sets,
                DriverMuscle(
                    slotById[plan.Id],
                    sets,
                    finalVolume,
                    targetByMuscleGroupId,
                    muscleNames)));
        }

        return adjustments;
    }

    private async Task<Dictionary<Guid, IReadOnlyList<MuscleLoad>>> LoadMuscleLoadsAsync(
        IReadOnlyList<Guid> exerciseIds,
        CancellationToken cancellationToken)
    {
        var muscles = await _db.ExerciseMuscles
            .AsNoTracking()
            .Where(muscle => exerciseIds.Contains(muscle.ExerciseId))
            .Select(muscle => new
            {
                muscle.ExerciseId,
                muscle.MuscleGroupId,
                muscle.Contribution
            })
            .ToListAsync(cancellationToken);

        return muscles
            .GroupBy(muscle => muscle.ExerciseId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<MuscleLoad>)group
                    .Select(muscle => new MuscleLoad(muscle.MuscleGroupId, muscle.Contribution))
                    .ToList());
    }

    /// <summary>
    /// Which muscle best explains a change to one exercise: the one that would sit furthest
    /// on the wrong side of its target had this exercise stayed on its prescription. Null
    /// when the exercise trains nothing the system has limits for.
    /// </summary>
    private static string? DriverMuscle(
        ExerciseSetSlot slot,
        int allocatedSets,
        IReadOnlyDictionary<Guid, decimal> finalVolume,
        IReadOnlyDictionary<Guid, MuscleVolumeTarget> targetByMuscleGroupId,
        IReadOnlyDictionary<Guid, string> muscleNames)
    {
        var direction = allocatedSets - slot.PrescribedSets;

        // Vežba vraćena tačno na svoj propis: predlog se korisniku jeste promenio, ali ga
        // ne objašnjava nijedan pojedinačan mišić — plan je samo prestao da odstupa.
        if (direction == 0)
        {
            return null;
        }

        Guid? driver = null;
        var widestGap = 0m;

        foreach (var muscle in slot.Muscles)
        {
            if (!targetByMuscleGroupId.TryGetValue(muscle.MuscleGroupId, out var target))
            {
                continue;
            }

            // Konačni volumen umanjen za ono što je baš ovo pomeranje donelo.
            var withoutTheMove = finalVolume.GetValueOrDefault(muscle.MuscleGroupId)
                                 + (muscle.Contribution * (slot.PrescribedSets - allocatedSets));

            var gap = direction > 0
                ? target.TargetSets - withoutTheMove
                : withoutTheMove - target.TargetSets;

            if (gap > widestGap)
            {
                widestGap = gap;
                driver = muscle.MuscleGroupId;
            }
        }

        return driver is null ? null : muscleNames.GetValueOrDefault(driver.Value);
    }
}

/// <summary>
/// One set proposal the volume balancing moved, for the message shown after a workout.
/// </summary>
/// <param name="SessionId">Session whose proposal changed.</param>
/// <param name="WeekNumber">Week that session belongs to.</param>
/// <param name="DayLabel">Day label of that session, e.g. "Push".</param>
/// <param name="ExerciseId">Exercise whose proposal changed.</param>
/// <param name="ExerciseName">Name of that exercise.</param>
/// <param name="FromSets">Sets proposed before this rebalance — what the lifter last saw.</param>
/// <param name="ToSets">Sets proposed now.</param>
/// <param name="Muscle">Muscle group whose weekly volume best explains the change.</param>
public sealed record SetAdjustment(
    Guid SessionId,
    int WeekNumber,
    string DayLabel,
    Guid ExerciseId,
    string ExerciseName,
    int FromSets,
    int ToSets,
    string? Muscle);
