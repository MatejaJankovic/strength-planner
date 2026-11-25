using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using StrengthPlanner.Application.DTOs.Mesocycles;
using StrengthPlanner.Application.DTOs.Sessions;
using StrengthPlanner.Application.DTOs.SetLogs;
using StrengthPlanner.Application.Exceptions;
using StrengthPlanner.Application.Interfaces;
using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Domain.Enums;
using StrengthPlanner.Infrastructure.Analytics;
using StrengthPlanner.Infrastructure.Exercises;
using StrengthPlanner.Infrastructure.Mesocycles;
using StrengthPlanner.Infrastructure.Persistence;

namespace StrengthPlanner.Infrastructure.TrainingLogs;

public class SessionService : ISessionService
{
    private readonly AppDbContext _db;
    private readonly VolumeLandmarkService _volumeLandmarks;
    private readonly DeloadService _deloads;
    private readonly WeeklySetPlanner _setPlanner;
    private readonly IMacrocycleService _macrocycles;
    private readonly SessionOutcomes _outcomes;
    private readonly E1RmCalculator _e1RmCalculator = new();

    public SessionService(
        AppDbContext db,
        VolumeLandmarkService volumeLandmarks,
        DeloadService deloads,
        WeeklySetPlanner setPlanner,
        IMacrocycleService macrocycles)
    {
        _db = db;
        _volumeLandmarks = volumeLandmarks;
        _deloads = deloads;
        _setPlanner = setPlanner;
        _macrocycles = macrocycles;
        _outcomes = new SessionOutcomes(db);
    }

    public async Task<WorkoutSessionDto> GetByIdAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await BuildSessionDetailsQuery(userId)
            .FirstOrDefaultAsync(workoutSession => workoutSession.Id == sessionId, cancellationToken);

        if (session is null)
        {
            throw new TrainingLogException(TrainingLogErrorType.NotFound, "Workout session was not found.");
        }

        return await ToDtoAsync(userId, session, cancellationToken);
    }

    public async Task<WorkoutSessionDto> StartAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await _db.WorkoutSessions
            .Include(workoutSession => workoutSession.TrainingWeek)
                .ThenInclude(week => week.Mesocycle)
            .FirstOrDefaultAsync(
                workoutSession => workoutSession.Id == sessionId
                                  && workoutSession.TrainingWeek.Mesocycle.UserId == userId,
                cancellationToken);

        if (session is null)
        {
            throw new TrainingLogException(TrainingLogErrorType.NotFound, "Workout session was not found.");
        }

        if (session.Status == SessionStatus.Completed)
        {
            throw new TrainingLogException(TrainingLogErrorType.Conflict, "Completed workout sessions cannot be started again.");
        }

        // Preskočen trening se ne pokreće prećutno. Nedelja je na osnovu njega već
        // zatvorena, pa je „predomislio sam se" odluka koja ima svoje dugme — inače bi
        // start na preskočenom treningu vratio 200 i ne bi promenio ništa.
        if (session.Status == SessionStatus.Skipped)
        {
            throw new TrainingLogException(
                TrainingLogErrorType.Conflict,
                "A skipped workout must be put back on the plan before it can be started.");
        }

        if (session.Status == SessionStatus.Planned)
        {
            session.Status = SessionStatus.InProgress;
            await _db.SaveChangesAsync(cancellationToken);
        }

        var detailedSession = await BuildSessionDetailsQuery(userId)
            .FirstAsync(workoutSession => workoutSession.Id == sessionId, cancellationToken);

        return await ToDtoAsync(userId, detailedSession, cancellationToken);
    }

    public async Task<WorkoutSessionDto> SkipAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var session = await LoadForLifecycleChangeAsync(userId, sessionId, cancellationToken);

        if (!SessionLifecycle.CanSkip(session.Status))
        {
            throw new TrainingLogException(
                TrainingLogErrorType.Conflict,
                "Only a workout that has not been started can be skipped.");
        }

        // Uslovni UPDATE, isto kao kod završetka: čitanje bez zaključavanja ne sprečava da
        // dva zahteva oba prođu, a sve ispod sme da se odigra tačno jednom.
        var claimed = await _db.WorkoutSessions
            .Where(candidate => candidate.Id == sessionId && candidate.Status == SessionStatus.Planned)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(candidate => candidate.Status, SessionStatus.Skipped),
                cancellationToken);

        if (claimed == 0)
        {
            throw new TrainingLogException(
                TrainingLogErrorType.Conflict,
                "Only a workout that has not been started can be skipped.");
        }

        // Granice volumena se NE obračunavaju: preskakanje nikada ne može da nedelju učini
        // u celosti odrađenom, a nedelja sa rupom o potrebnom volumenu ne govori ništa.
        //
        // Ocena umora i prelazak na sledeći blok se pokreću, jer oba pitaju da li je nešto
        // preostalo — a sada ne preostaje. To je cela poenta ove operacije.
        await _deloads.EvaluatePendingWeeksAsync(
            userId,
            session.TrainingWeek.MesocycleId,
            cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        await AdvanceBlockIfFinishedAsync(
            userId,
            session.TrainingWeek.MesocycleId,
            DateTime.UtcNow,
            transaction,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return await ReadBackAsync(userId, sessionId, cancellationToken);
    }

    public async Task<WorkoutSessionDto> UnskipAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await LoadForLifecycleChangeAsync(userId, sessionId, cancellationToken);

        if (!SessionLifecycle.CanUnskip(session.Status))
        {
            throw new TrainingLogException(
                TrainingLogErrorType.Conflict,
                "Only a skipped workout can be put back on the plan.");
        }

        // Ništa se ne poništava unazad. Auto-deload koji je nedelja u međuvremenu dobila
        // ostaje, i sledeći blok koji je generisan ostaje — oba su se desila zato što u tom
        // trenutku zaista nije bilo šta da se čeka, i oba su upisana u istoriju vežbača.
        // Vraćanje treninga na plan znači da će se odraditi, ne da se prošlost prepisuje.
        await _db.WorkoutSessions
            .Where(candidate => candidate.Id == sessionId && candidate.Status == SessionStatus.Skipped)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(candidate => candidate.Status, SessionStatus.Planned),
                cancellationToken);

        return await ReadBackAsync(userId, sessionId, cancellationToken);
    }

    /// <summary>
    /// Generiše sledeći blok plana kada od tekućeg više ništa ne preostaje.
    ///
    /// Ide poslednje, i njegov neuspeh ne sme da obori operaciju koja ga je pokrenula.
    /// Generator odbija šablon kome neka vežba više ne postoji (obrisana lična vežba,
    /// promenjen seed), a kako se sve dešava u istoj transakciji, izuzetak bi poništio i
    /// status sesije i e1RM zapise — pa vežbač svoj trening ne bi mogao da završi nikada,
    /// zbog usputne pogodnosti. Blok ostaje negenerisan i biće preuzet pri sledećem čitanju
    /// plana.
    ///
    /// Povratak na savepoint, a ne samo hvatanje izuzetka: da je pukla neka SQL naredba,
    /// cela transakcija bi u PostgreSQL-u bila u prekinutom stanju i commit bi svejedno pao.
    /// </summary>
    private async Task<MacrocycleAdvance?> AdvanceBlockIfFinishedAsync(
        Guid userId,
        Guid mesocycleId,
        DateTime now,
        IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string advanceSavepoint = "before_block_advance";
        await transaction.CreateSavepointAsync(advanceSavepoint, cancellationToken);

        try
        {
            var advance = await _macrocycles.AdvanceIfFinishedAsync(
                userId,
                mesocycleId,
                now,
                cancellationToken);

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.ReleaseSavepointAsync(advanceSavepoint, cancellationToken);

            return advance;
        }
        catch (MesocycleGenerationException)
        {
            await transaction.RollbackToSavepointAsync(advanceSavepoint, cancellationToken);
            return null;
        }
    }

    private async Task<WorkoutSession> LoadForLifecycleChangeAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var session = await _db.WorkoutSessions
            .AsNoTracking()
            .Include(workoutSession => workoutSession.TrainingWeek)
            .FirstOrDefaultAsync(
                workoutSession => workoutSession.Id == sessionId
                                  && workoutSession.TrainingWeek.Mesocycle.UserId == userId,
                cancellationToken);

        if (session is null)
        {
            throw new TrainingLogException(TrainingLogErrorType.NotFound, "Workout session was not found.");
        }

        return session;
    }

    private async Task<WorkoutSessionDto> ReadBackAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var session = await BuildSessionDetailsQuery(userId)
            .FirstAsync(workoutSession => workoutSession.Id == sessionId, cancellationToken);

        return await ToDtoAsync(userId, session, cancellationToken);
    }

    public async Task<CompleteSessionResultDto> CompleteAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var session = await ClaimForCompletionAsync(userId, sessionId, cancellationToken);
        var now = DateTime.UtcNow;

        var nextPlans = await _outcomes.LoadNextPlansAsync(session, cancellationToken);

        // Status je već upisan uslovnim UPDATE-om; ovo samo usklađuje učitanu instancu
        // sa bazom da bi DTO ispod prijavio tačno stanje.
        session.Status = SessionStatus.Completed;

        var outcome = await _outcomes.ApplyAsync(userId, session, nextPlans, now, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        var autoDeload = await CloseWeekAsync(userId, session, now, cancellationToken);

        // Rezime se zaključuje TEK ovde: sve do ovog trenutka je moglo da prepiše težinu
        // naredne nedelje — progresija, pravilo za narednu nedelju, pa i auto-deload.
        outcome.Finalize();

        // Sve što se tiče samog treninga mora da bude upisano pre prelaska na sledeći
        // blok — generator ispod poziva svoj SaveChanges, pa se na njega ne oslanjamo.
        await _db.SaveChangesAsync(cancellationToken);

        var volumeAdjustments = await RebalanceRestOfWeekAsync(userId, session, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        // Kada je ovim treningom ceo blok zaokružen, sledeći iz plana se generiše odmah,
        // od 1RM vrednosti koje važe sada. Ide poslednje: tek posle progresije i deload-a
        // je stanje bloka konačno.
        //
        // Neuspeh ovde ne sme da obori završetak treninga. Generator odbija šablon kome
        // neka vežba više ne postoji (obrisana custom vežba, promenjen seed), a kako se
        // ovo dešava u istoj transakciji, izuzetak bi poništio i status sesije i e1RM
        // zapise — pa korisnik svoj trening ne bi mogao da završi nikada, zbog usputne
        // pogodnosti. Blok ostaje negenerisan i biće preuzet pri sledećem pokušaju.
        var nextBlock = await AdvanceBlockIfFinishedAsync(
            userId,
            session.TrainingWeek.MesocycleId,
            now,
            transaction,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return BuildCompletionResult(session, outcome, autoDeload, volumeAdjustments, nextBlock);
    }

    /// <summary>
    /// Loads the session and takes it over for completion.
    ///
    /// The check on the status is only a quick exit: a read without a lock does not stop two
    /// simultaneous requests from both passing it. The conditional UPDATE claims the session
    /// atomically — the second request waits its turn and gets zero rows, so progression, the
    /// e1RM records and the volume limits are counted exactly once.
    /// </summary>
    private async Task<WorkoutSession> ClaimForCompletionAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var session = await _db.WorkoutSessions
            .Include(workoutSession => workoutSession.TrainingWeek)
                .ThenInclude(week => week.Mesocycle)
            .Include(workoutSession => workoutSession.ExercisePlans)
                .ThenInclude(plan => plan.Exercise)
            .Include(workoutSession => workoutSession.ExercisePlans)
                .ThenInclude(plan => plan.SetLogs)
            .FirstOrDefaultAsync(
                workoutSession => workoutSession.Id == sessionId
                                  && workoutSession.TrainingWeek.Mesocycle.UserId == userId,
                cancellationToken);

        if (session is null)
        {
            throw new TrainingLogException(TrainingLogErrorType.NotFound, "Workout session was not found.");
        }

        if (session.Status == SessionStatus.Completed)
        {
            throw new TrainingLogException(TrainingLogErrorType.Conflict, "Workout session is already completed.");
        }

        var claimed = await _db.WorkoutSessions
            .Where(candidate => candidate.Id == sessionId && candidate.Status != SessionStatus.Completed)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(candidate => candidate.Status, SessionStatus.Completed),
                cancellationToken);

        if (claimed == 0)
        {
            throw new TrainingLogException(TrainingLogErrorType.Conflict, "Workout session is already completed.");
        }

        return session;
    }

    /// <summary>
    /// What the week's end does: the volume limits learn from every week that is fully done
    /// and not yet counted, and the fatigue score may pull a deload forward.
    ///
    /// Both are conditional UPDATEs on the week, so a week is counted exactly once. They stay
    /// in the same transaction as the completion, so either everything is written or nothing.
    /// The fatigue score goes AFTER the progression: a deload rewrites the loads that the
    /// progression has just filled in for the next week.
    /// </summary>
    private async Task<DeloadOutcome?> CloseWeekAsync(
        Guid userId,
        WorkoutSession session,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await _volumeLandmarks.AdaptPendingWeeksAsync(
            userId,
            session.TrainingWeek.MesocycleId,
            now,
            cancellationToken);

        return await _deloads.EvaluatePendingWeeksAsync(
            userId,
            session.TrainingWeek.MesocycleId,
            cancellationToken);
    }

    /// <summary>
    /// The set plan is recalculated AFTER the deload, and only once the deload is WRITTEN.
    /// Both parts of that order carry weight:
    ///
    /// After the deload, because a deload changes the prescription the balancing starts from.
    /// After the write, because balancing asks the database which weeks are deloads — and
    /// DeloadService leaves the conversion of a week in the change tracker. Without the
    /// SaveChanges before this call the query still sees the old state, takes the freshly
    /// relieved week for an ordinary one and gives it back four sets in place of the halved
    /// two: the user gets a "deload" with the volume of a full training week. Measured before
    /// the fix — the whole week went from two to four sets per exercise.
    ///
    /// This workout wrote part of the week's volume (or skipped it), so the workouts still to
    /// come in that week get a proposal that brings the week back into the target zone. A
    /// block that follows its template literally is not balanced here either. Had it been
    /// skipped only at generation, the first completed workout would have put the sets back on
    /// the target volume — and the choice would have held exactly until that first workout.
    /// </summary>
    private async Task<IReadOnlyList<SetAdjustment>> RebalanceRestOfWeekAsync(
        Guid userId,
        WorkoutSession session,
        CancellationToken cancellationToken)
    {
        if (session.TrainingWeek.Mesocycle.SetAllocation != SetAllocation.TargetVolume)
        {
            return [];
        }

        return await _setPlanner.RebalanceAsync(
            userId,
            session.TrainingWeek.MesocycleId,
            cancellationToken);
    }

    private static CompleteSessionResultDto BuildCompletionResult(
        WorkoutSession session,
        SessionOutcome outcome,
        DeloadOutcome? autoDeload,
        IReadOnlyList<SetAdjustment> volumeAdjustments,
        MacrocycleAdvance? nextBlock)
    {
        return new CompleteSessionResultDto
        {
            SessionId = session.Id,
            Status = session.Status.ToString(),
            Exercises = outcome.Summaries,
            // Prijavljuje se samo tekuća nedelja. Balansiranje dodiruje i one koje tek
            // dolaze (granice volumena su se možda pomerile ovim treningom), ali posledica
            // OVOG treninga koju korisnik može da vidi na svom planu je ono što mu preostaje
            // u ovoj nedelji.
            VolumeAdjustments = volumeAdjustments
                .Where(adjustment => adjustment.WeekNumber == session.TrainingWeek.WeekNumber)
                .Select(adjustment => new SetAdjustmentDto
                {
                    SessionId = adjustment.SessionId,
                    DayLabel = adjustment.DayLabel,
                    ExerciseName = adjustment.ExerciseName,
                    FromSets = adjustment.FromSets,
                    ToSets = adjustment.ToSets,
                    Muscle = adjustment.Muscle,
                    Reason = adjustment.Reason?.ToString()
                })
                .ToList(),
            AutoDeload = autoDeload is null
                ? null
                : new AutoDeloadDto
                {
                    TriggeredByWeek = autoDeload.TriggeredByWeek,
                    DeloadWeek = autoDeload.DeloadWeek,
                    FatigueScore = autoDeload.FatigueScore,
                    PlannedDeloadReleasedWeek = autoDeload.PlannedDeloadReleasedWeek
                },
            NextBlock = nextBlock is null
                ? null
                : new MacrocycleAdvanceDto
                {
                    PlanName = nextBlock.PlanName,
                    BlockOrder = nextBlock.BlockOrder,
                    BlockCount = nextBlock.BlockCount,
                    Goal = nextBlock.Goal.ToString(),
                    MesocycleId = nextBlock.MesocycleId,
                    MesocycleName = nextBlock.MesocycleName
                }
        };
    }

    /// <summary>Body mass the exercises of this session carry for the lifter right now.</summary>
    /// <summary>
    /// Jedini put od sesije do njenog DTO-a.
    ///
    /// Tri čitanja su ranije sama sastavljala isti poziv, i to je tačno oblik greške iz
    /// devete runde (<c>SetLogDto</c>, građen na tri mesta, gde je nova kolona stigla do
    /// dva): ručno sastavljanje ne mora da bude potpuno, pa se prevodi i tipizira i kad
    /// jedno mesto zaostane.
    /// </summary>
    private async Task<WorkoutSessionDto> ToDtoAsync(
        Guid userId,
        WorkoutSession session,
        CancellationToken cancellationToken)
    {
        var weightStepOverrides = await WeightStepResolver.LoadOverridesAsync(_db, userId, cancellationToken);
        var portions = await ResolvePortionsAsync(userId, session, cancellationToken);

        // Nedelje bloka se čitaju posebno: Include od nedelje do bloka i nazad do njegovih
        // nedelja je ciklus, a njega EF u upitu bez praćenja odbija.
        var weeks = await _db.TrainingWeeks
            .AsNoTracking()
            .Where(week => week.MesocycleId == session.TrainingWeek.MesocycleId
                           && week.Mesocycle.UserId == userId)
            .Select(week => new { week.WeekNumber, week.IsDeload })
            .ToListAsync(cancellationToken);
        var isLastTrainingWeek = CalibrationSet.IsLastTrainingWeek(
            session.TrainingWeek.WeekNumber,
            session.TrainingWeek.IsDeload,
            weeks.Select(week => (week.WeekNumber, week.IsDeload)));

        return ToDto(
            session,
            weightStepOverrides,
            portions,
            await ResolveEstimatedTargetsAsync(userId, session, portions, weightStepOverrides, cancellationToken),
            isLastTrainingWeek);
    }

    /// <summary>
    /// Predlog opterećenja za plan koji svoj cilj još nema upisan.
    ///
    /// Generator puni samo prvu nedelju; kasnije puni progresija, kada se isti dan
    /// prethodne nedelje završi. Do tada je <c>TargetWeightKg</c> prazan — pa je ekran
    /// pisao „Nema 1RM za ovu vežbu" i za vežbu čiji maksimum stoji zapisan. Isto se
    /// dešavalo i unutar jedne nedelje: sveža procena sa ponedeljka nije dolazila do
    /// četvrtka, jer se propis prenosi samo sa istog dana prethodne nedelje.
    ///
    /// Odgovor se računa pri čitanju, a ne upisuje: tako uvek polazi od najsvežijeg
    /// maksimuma, a u nedelje do kojih vežbač nije stigao se ne upisuju brojevi koje bi
    /// progresija ionako prepisala.
    ///
    /// Izolacija u opsegu preko 12 ponavljanja je izuzetak: njene serije većinom ne upisuju
    /// procenu, pa je maksimum na zapisu stariji od onoga što je već digla. Za nju se predlog
    /// prenosi sa poslednje upisane težine iste vežbe u ovom bloku, istim pravilom kojim se
    /// prenosi i posle završenog treninga (<see cref="NextWeekLoad"/>).
    /// </summary>
    private async Task<Dictionary<Guid, EstimatedTarget>> ResolveEstimatedTargetsAsync(
        Guid userId,
        WorkoutSession session,
        IReadOnlyDictionary<Guid, decimal> bodyweightPortions,
        IReadOnlyDictionary<Guid, decimal> weightStepOverrides,
        CancellationToken cancellationToken)
    {
        // Samo za trening koji tek predstoji. Završen ili preskočen nosi istoriju: predlog
        // za nešto što se već desilo (ili neće) nije predlog nego šum — a i upit ispod bi se
        // plaćao pri svakom listanju istorije.
        if (!SessionLifecycle.IsPending(session.Status))
        {
            return [];
        }

        var missing = session.ExercisePlans
            .Where(plan => plan.TargetWeightKg is null)
            .ToList();

        if (missing.Count == 0)
        {
            return [];
        }

        var exerciseIds = missing.Select(plan => plan.ExerciseId).Distinct().ToList();

        var records = await _db.OneRepMaxRecords
            .AsNoTracking()
            .Where(record => record.UserId == userId && exerciseIds.Contains(record.ExerciseId))
            .Select(record => new { record.ExerciseId, record.ValueKg, record.Source, record.RecordedAt })
            .ToListAsync(cancellationToken);

        // Isti izbor osnovne vrednosti koji koristi generator: najbolja u prozoru, ručni
        // unos poništava starije procene, prazan prozor pada na najnoviji zapis ikada.
        var now = DateTime.UtcNow;
        var oneRepMaxByExerciseId = records
            .GroupBy(record => record.ExerciseId)
            .Select(group => new
            {
                ExerciseId = group.Key,
                ValueKg = OneRepMaxBaseline.Select(
                    group
                        .Select(record => new OneRepMaxSample(record.ValueKg, record.Source, record.RecordedAt))
                        .ToList(),
                    now,
                    TrainingConstants.OneRepMaxLookbackDays,
                    allowStaleFallback: true)
            })
            .Where(entry => entry.ValueKg is not null)
            .ToDictionary(entry => entry.ExerciseId, entry => entry.ValueKg!.Value);

        var carriedIds = missing
            .Where(plan => plan.RepRangeMax > TrainingConstants.EpleyRepCap)
            .Select(plan => plan.ExerciseId)
            .Distinct()
            .ToList();

        var week = session.TrainingWeek;
        var earlierTargets = carriedIds.Count == 0
            ? []
            : await _db.ExercisePlans
                .AsNoTracking()
                .Where(plan => carriedIds.Contains(plan.ExerciseId)
                               && plan.TargetWeightKg != null
                               && plan.WorkoutSession.TrainingWeek.MesocycleId == week.MesocycleId
                               && plan.WorkoutSession.TrainingWeek.Mesocycle.UserId == userId
                               && plan.WorkoutSession.TrainingWeek.WeekNumber <= week.WeekNumber
                               && !plan.WorkoutSession.TrainingWeek.IsDeload)
                .Select(plan => new
                {
                    plan.ExerciseId,
                    plan.WorkoutSession.DayLabel,
                    plan.WorkoutSession.TrainingWeek.WeekNumber,
                    TargetWeightKg = plan.TargetWeightKg!.Value,
                    Prescription = new LoadPrescription(plan.RepRangeMin, plan.RepRangeMax, plan.TargetRir)
                })
                .ToListAsync(cancellationToken);

        var estimates = new Dictionary<Guid, EstimatedTarget>();

        foreach (var plan in missing)
        {
            var oneRepMax = oneRepMaxByExerciseId.TryGetValue(plan.ExerciseId, out var value) ? value : (decimal?)null;
            var bodyweightLoadKg = BodyweightPortionResolver.PortionFor(bodyweightPortions, plan.ExerciseId);
            var weightStepKg = WeightStepResolver.Effective(weightStepOverrides, plan.ExerciseId, plan.Exercise.WeightStepKg);

            // Najbliža ranija težina: najkasnija nedelja, a u njoj isti dan pre drugog.
            var earlier = earlierTargets
                .Where(target => target.ExerciseId == plan.ExerciseId)
                .OrderByDescending(target => target.WeekNumber)
                .ThenByDescending(target => target.DayLabel == session.DayLabel)
                .FirstOrDefault();

            if (plan.RepRangeMax > TrainingConstants.EpleyRepCap && earlier is not null)
            {
                var carried = NextWeekLoad.For(
                    earlier.TargetWeightKg,
                    progressionWeightKg: null,
                    earlier.Prescription,
                    SessionLoadInputs.PrescriptionOf(plan),
                    week.IsDeload,
                    oneRepMax,
                    weightStepKg,
                    bodyweightLoadKg);

                if (carried is not null)
                {
                    estimates[plan.Id] = new EstimatedTarget(carried.Value, FromMaximum: false);
                    continue;
                }
            }

            var estimate = StartingLoad.FromOneRepMax(
                _e1RmCalculator,
                oneRepMax,
                plan.RepRangeMin,
                plan.TargetRir,
                bodyweightLoadKg,
                weightStepKg);

            if (estimate is not null)
            {
                estimates[plan.Id] = new EstimatedTarget(estimate.Value, FromMaximum: true);
            }
        }

        return estimates;
    }

    /// <summary>A target computed on read, and whether it came from a maximum on file.</summary>
    private sealed record EstimatedTarget(decimal WeightKg, bool FromMaximum);

    private Task<IReadOnlyDictionary<Guid, decimal>> ResolvePortionsAsync(
        Guid userId,
        WorkoutSession session,
        CancellationToken cancellationToken)
    {
        return BodyweightPortionResolver.ResolveAsync(
            _db,
            userId,
            session.ExercisePlans.Select(plan => plan.ExerciseId).Distinct().ToList(),
            cancellationToken);
    }

    private IQueryable<WorkoutSession> BuildSessionDetailsQuery(Guid userId)
    {
        return _db.WorkoutSessions
            .AsNoTracking()
            .AsSplitQuery()
            .Include(workoutSession => workoutSession.TrainingWeek)
                .ThenInclude(week => week.Mesocycle)
            .Include(workoutSession => workoutSession.ExercisePlans)
                .ThenInclude(plan => plan.Exercise)
            .Include(workoutSession => workoutSession.ExercisePlans)
                .ThenInclude(plan => plan.SetLogs)
            .Where(workoutSession => workoutSession.TrainingWeek.Mesocycle.UserId == userId);
    }

    private static WorkoutSessionDto ToDto(
        WorkoutSession session,
        IReadOnlyDictionary<Guid, decimal> weightStepOverrides,
        IReadOnlyDictionary<Guid, decimal> bodyweightPortions,
        IReadOnlyDictionary<Guid, EstimatedTarget> estimatedTargets,
        bool isLastTrainingWeek)
    {
        var mesocycle = session.TrainingWeek.Mesocycle;

        return new WorkoutSessionDto
        {
            Id = session.Id,
            WeekNumber = session.TrainingWeek.WeekNumber,
            IsDeload = session.TrainingWeek.IsDeload,
            IsAutoDeload = session.TrainingWeek.IsAutoDeload,
            DayLabel = session.DayLabel,
            Date = session.Date,
            Status = session.Status.ToString(),
            ExercisePlans = session.ExercisePlans
                .OrderBy(plan => plan.Order)
                .Select(plan => new ExercisePlanDto
                {
                    Id = plan.Id,
                    ExerciseId = plan.ExerciseId,
                    ExerciseName = plan.Exercise.Name,
                    Order = plan.Order,
                    TargetSets = plan.TargetSets,
                    PrescribedSets = plan.PrescribedSets,
                    RepRangeMin = plan.RepRangeMin,
                    RepRangeMax = plan.RepRangeMax,
                    TargetRir = plan.TargetRir,
                    TargetWeightKg = plan.TargetWeightKg
                                     ?? (estimatedTargets.TryGetValue(plan.Id, out var estimate)
                                         ? estimate.WeightKg
                                         : null),
                    // Razlika koju vežbač treba da vidi: cilj izveden iz maksimuma nije isto
                    // što i cilj koji je progresija izvela iz odrađenih serija.
                    TargetWeightIsEstimate = plan.TargetWeightKg is null
                                             && estimatedTargets.ContainsKey(plan.Id),
                    TargetWeightIsCarried = plan.TargetWeightKg is null
                                            && estimatedTargets.TryGetValue(plan.Id, out var carried)
                                            && !carried.FromMaximum,
                    WeightStepKg = WeightStepResolver.Effective(
                        weightStepOverrides,
                        plan.ExerciseId,
                        plan.Exercise.WeightStepKg),
                    Equipment = plan.Exercise.Equipment,
                    IsBodyweight = BodyweightPortionResolver.PortionFor(bodyweightPortions, plan.ExerciseId) > 0,
                    BodyweightLoadKg = BodyweightPortionResolver.PortionFor(bodyweightPortions, plan.ExerciseId),
                    IsCalibration = CalibrationSet.AppliesTo(
                        mesocycle.ExperienceLevel,
                        isLastTrainingWeek,
                        plan.Order,
                        plan.Exercise.Type,
                        plan.Exercise.SuitsLowReps,
                        plan.RepRangeMax,
                        plan.TargetRir),
                    SetLogs = plan.SetLogs
                        .OrderBy(set => set.SetNumber)
                        .Select(SetLogMapper.ToDto)
                        .ToList()
                })
                .ToList()
        };
    }
}
