using Microsoft.EntityFrameworkCore;
using StrengthPlanner.Application.DTOs.Sessions;
using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Domain.Enums;
using StrengthPlanner.Infrastructure.Exercises;
using StrengthPlanner.Infrastructure.Persistence;

namespace StrengthPlanner.Infrastructure.TrainingLogs;

/// <summary>
/// What a finished workout does to its exercises: the estimated maximum it produced, the load
/// progression it earned, and the target it writes into the same day of the next week.
///
/// This used to be the first half of <c>SessionService.CompleteAsync</c>. It is its own step
/// because it has to be callable on its own: it does not close the week, score fatigue, learn
/// volume limits or rebalance sets, and those happen after it, in an order that carries weight
/// (see the comments in <see cref="SessionService"/>).
/// </summary>
public sealed class SessionOutcomes
{
    private readonly AppDbContext _db;
    private readonly E1RmCalculator _e1RmCalculator = new();
    private readonly ProgressionEngine _progressionEngine = new();

    public SessionOutcomes(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// The plans of the same day in later weeks of the block that are still open for a new
    /// target, nearest week first.
    ///
    /// Only sessions that are not completed: a completion out of order must not overwrite the
    /// targets of workouts that were already done.
    /// </summary>
    public async Task<IReadOnlyList<ExercisePlan>> LoadNextPlansAsync(
        Guid userId,
        WorkoutSession session,
        CancellationToken cancellationToken)
    {
        var exerciseIds = ExerciseIdsOf(session);

        return await _db.ExercisePlans
            .Include(plan => plan.WorkoutSession)
                .ThenInclude(workoutSession => workoutSession.TrainingWeek)
            .Where(plan => plan.WorkoutSession.TrainingWeek.MesocycleId == session.TrainingWeek.MesocycleId
                           && plan.WorkoutSession.TrainingWeek.Mesocycle.UserId == userId
                           && plan.WorkoutSession.DayLabel == session.DayLabel
                           && plan.WorkoutSession.TrainingWeek.WeekNumber > session.TrainingWeek.WeekNumber
                           && plan.WorkoutSession.Status != SessionStatus.Completed
                           && exerciseIds.Contains(plan.ExerciseId))
            .OrderBy(plan => plan.WorkoutSession.TrainingWeek.WeekNumber)
            .ThenBy(plan => plan.Order)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Writes the estimate and the next week's target for every exercise of the session, and
    /// returns one summary per exercise.
    ///
    /// The summaries are not final: the week may still become a deload and the sets may still
    /// be rebalanced, and either rewrites the target on the very same tracked entities. Call
    /// <see cref="SessionOutcome.Settle"/> once nothing can change it any more.
    /// </summary>
    public async Task<SessionOutcome> ApplyAsync(
        Guid userId,
        WorkoutSession session,
        IReadOnlyList<ExercisePlan> nextPlans,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var exerciseIds = ExerciseIdsOf(session);
        var weightStepByExerciseId = await WeightStepResolver.ResolveAsync(
            _db,
            userId,
            exerciseIds,
            cancellationToken);
        // Deo telesne mase kakav vazi SADA. Ono sto je odradjeno nosi svoj snimak (SetLog),
        // ali propis za narednu nedelju gleda unapred, pa polazi od mase iz profila.
        var bodyweightByExerciseId = await BodyweightPortionResolver.ResolveAsync(
            _db,
            userId,
            exerciseIds,
            cancellationToken);

        // Za preračun opterećenja se gleda samo skorašnji prozor, isto kao pri generisanju
        // bloka. Rekord od pre pola godine je istorija, a ne procena trenutne snage — a
        // ovde bi postao ciljno opterećenje naredne nedelje. Sirov maksimum prozora je
        // zamenjen pravilom iz domena (OneRepMaxBaseline): jedna naduvana procena ne sme
        // osam nedelja da bude polazna težina.
        var samplesByExerciseId = (await _db.OneRepMaxRecords
                .AsNoTracking()
                .Where(record => record.UserId == userId && exerciseIds.Contains(record.ExerciseId))
                .Select(record => new
                {
                    record.ExerciseId,
                    record.ValueKg,
                    record.Source,
                    record.RecordedAt
                })
                .ToListAsync(cancellationToken))
            .GroupBy(record => record.ExerciseId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<OneRepMaxSample>)group
                    .Select(record => new OneRepMaxSample(record.ValueKg, record.Source, record.RecordedAt))
                    .ToList());

        // Najveći dosadašnji maksimum po vežbi, za oznaku rekorda. Izvodi se iz istih zapisa
        // kao i polazna težina: poseban upit sa Max nad decimalom ne može da se izvrši na
        // SQLite-u koji koristi probni sklop, a nije bio potreban.
        var previousMaxByExerciseId = samplesByExerciseId.ToDictionary(
            entry => entry.Key,
            entry => entry.Value.Max(sample => sample.ValueKg));

        var summaries = new List<CompletedExerciseSummaryDto>();

        // Deload je pauza, ne korak nazad: progresija za nedelju posle njega polazi od
        // poslednje trenažne nedelje, a ne od olakšanih serija samog deload-a. Nedelja
        // posle deload-a postoji kad je umor povukao rasterećenje ranije i time oslobodio
        // planirano (vidi DeloadService.RestorePlannedDeloadAsync).
        var resumePoints = session.TrainingWeek.IsDeload
            ? await LoadResumePointsAsync(userId, session, weightStepByExerciseId, cancellationToken)
            : new Dictionary<Guid, ResumePoint>();

        foreach (var plan in session.ExercisePlans.OrderBy(plan => plan.Order))
        {
            var nextPlan = nextPlans.FirstOrDefault(candidate => candidate.ExerciseId == plan.ExerciseId);
            var logs = plan.SetLogs
                .OrderBy(set => set.SetNumber)
                .ToList();

            var summary = new CompletedExerciseSummaryDto
            {
                ExercisePlanId = plan.Id,
                ExerciseId = plan.ExerciseId,
                ExerciseName = plan.Exercise.Name
            };

            var weightStepKg = WeightStepResolver.StepFor(weightStepByExerciseId, plan.ExerciseId);

            // Referentna težina je najteža koju je vežbač podigao, a ne prosek svih serija:
            // prosek je jednu back-off seriju pretvarao u niži predlog od težine koja je u
            // istom treningu podignuta više puta.
            var load = WorkingLoad.Select(logs.Select(SessionLoadInputs.ToLoggedSet).ToList());

            // Deload serije su namerno submaksimalne — njihov e1RM bi veštački
            // oborio trend snage i start sledećeg mezociklusa, pa se ne upisuje.
            var bestEstimate = load is null || session.TrainingWeek.IsDeload
                ? null
                : EstimateBestOneRepMax(logs);
            if (bestEstimate.HasValue)
            {
                summary.E1Rm = bestEstimate.Value;
                summary.IsPr = previousMaxByExerciseId.TryGetValue(plan.ExerciseId, out var previousMax)
                               && bestEstimate.Value > previousMax;

                _db.OneRepMaxRecords.Add(new OneRepMaxRecord
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    ExerciseId = plan.ExerciseId,
                    ValueKg = bestEstimate.Value,
                    Source = OneRepMaxSource.Estimated,
                    RecordedAt = now,
                    WorkoutSessionId = session.Id
                });
            }

            // Progresija sudi o onome sto je podignuto, pa uzima snimak referentne serije,
            // a ne trenutnu masu iz profila: promena mase u profilu ne sme naknadno da
            // promeni kako je prosao trening od prosle nedelje.
            var progression = load is null
                ? null
                : _progressionEngine.ComputeNext(
                    load.ReferenceWeightKg,
                    load.WorkingSets,
                    plan.TargetRir,
                    plan.RepRangeMin,
                    plan.RepRangeMax,
                    weightStepKg,
                    load.ReferenceBodyweightLoadKg);

            // Ono što je vežbač podigao u OVOM treningu; za preskočenu vežbu planirana
            // težina. Iz toga se na kraju računa razlika koju rezime prikazuje.
            summary.UsedWeightKg = load?.ReferenceWeightKg ?? plan.TargetWeightKg;
            summary.BodyweightLoadKg = load?.ReferenceBodyweightLoadKg
                                       ?? BodyweightPortionResolver.PortionFor(bodyweightByExerciseId, plan.ExerciseId);
            summary.IsBodyweight = summary.BodyweightLoadKg > 0;
            summary.LoadFloorReached = progression?.LoadFloorReached ?? false;

            var referenceWeightKg = summary.UsedWeightKg;
            var progressionWeightKg = progression?.NextWeightKg;
            var currentPrescription = SessionLoadInputs.PrescriptionOf(plan);
            var nextBodyweightLoadKg = BodyweightPortionResolver.PortionFor(
                bodyweightByExerciseId,
                plan.ExerciseId);

            // Važi i kad je naredna nedelja opet deload (planirani koji je već počeo, pa
            // ga auto-deload nije oslobodio): bez ovoga bi se 90% primenilo na već
            // rasterećenu težinu i druga deload nedelja bi pala na 81%.
            if (session.TrainingWeek.IsDeload && nextPlan is not null)
            {
                if (resumePoints.TryGetValue(plan.ExerciseId, out var resume))
                {
                    currentPrescription = resume.Prescription;
                    referenceWeightKg = resume.ReferenceWeightKg;
                    progressionWeightKg = resume.ProgressionWeightKg;
                }
                else
                {
                    // Nema odrađene trenažne nedelje za ovaj dan (npr. cela je preskočena),
                    // pa se referenca vraća iz same deload težine: ona nosi 90% prethodne.
                    referenceWeightKg = NextWeekLoad.UndoDeload(
                        plan.TargetWeightKg,
                        weightStepKg,
                        nextBodyweightLoadKg);
                    progressionWeightKg = null;

                    // Vraćena težina je trenažna, pa ide uz RIR cilja, a ne uz RIR deload-a:
                    // prevod preko deload RIR-a bi je naduvao za dve rezerve (kablovsko
                    // letenje 30 kg bi dobilo 32.5 - korak koji niko nije zaradio).
                    currentPrescription = currentPrescription with
                    {
                        TargetRir = GoalPrescriptions.ForGoal(session.TrainingWeek.Mesocycle.Goal).TargetRir
                    };
                }
            }

            if (nextPlan is not null)
            {
                // Procena iz ovog treninga ulazi kao još jedan uzorak, a ne kao presuda:
                // pravilo je bira samo ako nije samotni ekstrem u prozoru.
                var samples = samplesByExerciseId.GetValueOrDefault(plan.ExerciseId, []).ToList();
                if (bestEstimate.HasValue)
                {
                    samples.Add(new OneRepMaxSample(bestEstimate.Value, OneRepMaxSource.Estimated, now));
                }

                var baselineOneRepMax = OneRepMaxBaseline.Select(
                    samples,
                    now,
                    TrainingConstants.OneRepMaxLookbackDays,
                    allowStaleFallback: false);

                var nextWeight = NextWeekLoad.For(
                    referenceWeightKg,
                    progressionWeightKg,
                    currentPrescription,
                    SessionLoadInputs.PrescriptionOf(nextPlan),
                    nextPlan.WorkoutSession.TrainingWeek.IsDeload,
                    baselineOneRepMax,
                    weightStepKg,
                    nextBodyweightLoadKg);

                // Null znači da o vežbi nema nijednog podatka; zatečeni cilj se tada ne
                // prepisuje praznom vrednošću.
                if (nextWeight is not null)
                {
                    nextPlan.TargetWeightKg = nextWeight;
                }
            }

            summaries.Add(summary);
        }

        return new SessionOutcome(summaries, nextPlans);
    }

    /// <summary>
    /// Where progression resumes from after a deload week, per exercise: the most recent
    /// completed training session of the same day, with its own prescription, reference load
    /// and progression result.
    /// </summary>
    private async Task<Dictionary<Guid, ResumePoint>> LoadResumePointsAsync(
        Guid userId,
        WorkoutSession deloadSession,
        IReadOnlyDictionary<Guid, decimal> weightStepByExerciseId,
        CancellationToken cancellationToken)
    {
        var plans = await _db.ExercisePlans
            .AsNoTracking()
            .Include(plan => plan.SetLogs)
            .Include(plan => plan.WorkoutSession)
            .Where(plan => plan.WorkoutSession.TrainingWeek.MesocycleId == deloadSession.TrainingWeek.MesocycleId
                           && plan.WorkoutSession.TrainingWeek.Mesocycle.UserId == userId
                           && plan.WorkoutSession.DayLabel == deloadSession.DayLabel
                           && plan.WorkoutSession.Status == SessionStatus.Completed
                           && !plan.WorkoutSession.TrainingWeek.IsDeload
                           && plan.WorkoutSession.TrainingWeek.WeekNumber < deloadSession.TrainingWeek.WeekNumber)
            .OrderByDescending(plan => plan.WorkoutSession.TrainingWeek.WeekNumber)
            .ToListAsync(cancellationToken);

        var resumePoints = new Dictionary<Guid, ResumePoint>();

        foreach (var plan in plans)
        {
            // Upit je poređan od najsvežije nedelje, pa prvi nalaz po vežbi i jeste onaj
            // od koga se nastavlja.
            if (resumePoints.ContainsKey(plan.ExerciseId))
            {
                continue;
            }

            var load = WorkingLoad.Select(plan.SetLogs
                .OrderBy(set => set.SetNumber)
                .Select(SessionLoadInputs.ToLoggedSet)
                .ToList());

            if (load is null)
            {
                continue;
            }

            var progression = _progressionEngine.ComputeNext(
                load.ReferenceWeightKg,
                load.WorkingSets,
                plan.TargetRir,
                plan.RepRangeMin,
                plan.RepRangeMax,
                WeightStepResolver.StepFor(weightStepByExerciseId, plan.ExerciseId),
                load.ReferenceBodyweightLoadKg);

            resumePoints[plan.ExerciseId] = new ResumePoint(
                SessionLoadInputs.PrescriptionOf(plan),
                load.ReferenceWeightKg,
                progression.NextWeightKg);
        }

        return resumePoints;
    }

    /// <summary>
    /// Best e1RM the session produced, or null when no set may produce one.
    ///
    /// The recorded <c>Rir</c> is used on purpose, not <see cref="WorkingSet.EffectiveRir"/>:
    /// Epley assumes a set to failure, so 0 is the right value for one, while effective RIR
    /// can go negative and serves auto-regulation alone. Which sets qualify is
    /// <see cref="E1RmCalculator.CanEstimateFrom"/>, shared with the fatigue score.
    /// </summary>
    private decimal? EstimateBestOneRepMax(IReadOnlyList<SetLog> logs)
    {
        return _e1RmCalculator.BestEstimate(logs.Select(SessionLoadInputs.ToLoggedSet));
    }

    private static List<Guid> ExerciseIdsOf(WorkoutSession session)
    {
        return session.ExercisePlans
            .Select(plan => plan.ExerciseId)
            .Distinct()
            .ToList();
    }

    /// <summary>Where progression stood before a deload week interrupted it.</summary>
    private sealed record ResumePoint(
        LoadPrescription Prescription,
        decimal ReferenceWeightKg,
        decimal ProgressionWeightKg);
}

/// <summary>
/// The per-exercise result of <see cref="SessionOutcomes.ApplyAsync"/>, together with the
/// plans of the next week it wrote targets into.
/// </summary>
public sealed record SessionOutcome(
    List<CompletedExerciseSummaryDto> Summaries,
    IReadOnlyList<ExercisePlan> NextPlans)
{
    /// <summary>
    /// Fills in what the summary reports about the next session, once nothing can change it
    /// any more.
    ///
    /// The proposal is read from the stored plan of the next week, not from the progression
    /// result: the week may have become a deload in the meantime, and that rewrites the
    /// target on the very same tracked entities. The old version only patched the summary
    /// when an auto-deload happened, and left the arrow from the progression result behind
    /// in every other case — including a planned deload, where the proposal is 90% of the
    /// load lifted and the summary still showed it rising.
    ///
    /// With no next week in this block there is no proposal to show: the next block derives
    /// its own weights from the estimated maximum when it is generated.
    /// </summary>
    public void Settle()
    {
        foreach (var summary in Summaries)
        {
            var nextPlan = NextPlans.FirstOrDefault(plan => plan.ExerciseId == summary.ExerciseId);

            summary.NextWeightKg = nextPlan?.TargetWeightKg;
            summary.WeightChangeKg = NextWeekLoad.ChangeKg(summary.UsedWeightKg, summary.NextWeightKg);
            summary.WeightIncreased = summary.WeightChangeKg > 0;
        }
    }
}

/// <summary>Translations from stored rows to the inputs the load algorithms take.</summary>
internal static class SessionLoadInputs
{
    public static LoadPrescription PrescriptionOf(ExercisePlan plan)
    {
        return new LoadPrescription(plan.RepRangeMin, plan.RepRangeMax, plan.TargetRir);
    }

    public static LoggedSet ToLoggedSet(SetLog set)
    {
        return new LoggedSet(set.WeightKg, set.Reps, set.Rir, set.IsFailure, set.BodyweightLoadKg);
    }
}
