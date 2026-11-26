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
using StrengthPlanner.Infrastructure.Notes;
using StrengthPlanner.Infrastructure.Persistence;

namespace StrengthPlanner.Infrastructure.TrainingLogs;

public class SessionService : ISessionService
{
    private readonly AppDbContext _db;
    private readonly DeloadService _deloads;
    private readonly IMacrocycleService _macrocycles;
    private readonly SessionOutcomes _outcomes;
    private readonly ISessionRevisionService? _revisions;
    private readonly WeekConsequences _week;
    private readonly E1RmCalculator _e1RmCalculator = new();

    public SessionService(
        AppDbContext db,
        VolumeLandmarkService volumeLandmarks,
        DeloadService deloads,
        WeeklySetPlanner setPlanner,
        IMacrocycleService macrocycles,
        ISessionRevisionService? revisions = null)
    {
        _db = db;
        _revisions = revisions;
        _deloads = deloads;
        _macrocycles = macrocycles;
        _outcomes = new SessionOutcomes(db);
        _week = new WeekConsequences(volumeLandmarks, deloads, setPlanner);
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
        await ApplyPendingCorrectionsAsync(userId, sessionId, cancellationToken);

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

        await ApplyPendingCorrectionsAsync(userId, sessionId, cancellationToken);

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

    /// <summary>
    /// A correction the lifter left unsaved is saved before anything that would use the sets up.
    /// Starting the next workout of the same day closes the window, and finishing or skipping
    /// the last workout of a block opens the next block from the estimates. Without this the
    /// sets would be changed and nothing built from them would be, and no later request could
    /// ever save the correction.
    /// </summary>
    private async Task ApplyPendingCorrectionsAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        if (_revisions is null)
        {
            return;
        }

        var mesocycleId = await _db.WorkoutSessions
            .AsNoTracking()
            .Where(session => session.Id == sessionId && session.TrainingWeek.Mesocycle.UserId == userId)
            .Select(session => (Guid?)session.TrainingWeek.MesocycleId)
            .FirstOrDefaultAsync(cancellationToken);

        if (mesocycleId is not null)
        {
            await _revisions.ApplyPendingAsync(userId, mesocycleId.Value, cancellationToken);
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
        int? perceivedFatigue,
        CancellationToken cancellationToken = default)
    {
        if (perceivedFatigue is { } rating && !SubjectiveFatigue.IsValid(rating))
        {
            throw new TrainingLogException(
                TrainingLogErrorType.Validation,
                $"Fatigue rating must be {SubjectiveFatigue.MinRating} to {SubjectiveFatigue.MaxRating}.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        await ApplyPendingCorrectionsAsync(userId, sessionId, cancellationToken);

        var session = await ClaimForCompletionAsync(userId, sessionId, cancellationToken);
        var now = DateTime.UtcNow;

        var nextPlans = await _outcomes.LoadNextPlansAsync(userId, session, cancellationToken);

        // Status je već upisan uslovnim UPDATE-om; ovo samo usklađuje učitanu instancu
        // sa bazom da bi DTO ispod prijavio tačno stanje.
        session.Status = SessionStatus.Completed;
        session.CompletedAt = now;

        // Odgovor se upisuje ovde, uz završetak, a ne naknadnim pozivom: ocena umora nedelje se računa kad se
        // završi njen poslednji trening, i odgovor iz tog treninga mora da bude u bazi do tada.
        session.PerceivedFatigue = perceivedFatigue;

        var outcome = await _outcomes.ApplyAsync(userId, session, nextPlans, now, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        var autoDeload = await _week.CloseAsync(userId, session.TrainingWeek.MesocycleId, now, cancellationToken);

        // Rezime se zaključuje TEK ovde: sve do ovog trenutka je moglo da prepiše težinu
        // naredne nedelje — progresija, pravilo za narednu nedelju, pa i auto-deload.
        outcome.Settle();

        // Sve što se tiče samog treninga mora da bude upisano pre prelaska na sledeći
        // blok — generator ispod poziva svoj SaveChanges, pa se na njega ne oslanjamo. Ovaj
        // upis je i preduslov balansiranja ispod: ono pita bazu koje su nedelje deload, a
        // DeloadService pretvaranje nedelje ostavlja u change trackeru (vidi
        // WeekConsequences.RebalanceAsync).
        await _db.SaveChangesAsync(cancellationToken);

        var volumeAdjustments = await _week.RebalanceAsync(
            userId,
            session.TrainingWeek.MesocycleId,
            session.TrainingWeek.Mesocycle.SetAllocation,
            cancellationToken);

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

        return CompletionResults.Build(session, outcome, autoDeload, volumeAdjustments, nextBlock);
    }

    /// <summary>
    /// Loads the session and takes it over for completion.
    ///
    /// Only a workout that is in progress can be completed. A planned one has not been started
    /// (and would be completed with no sets, writing a week's worth of consequences from
    /// nothing), and a skipped one is an answer the lifter gave: completing it behind the
    /// screen's back would turn that answer into a finished workout. Both are refused, as the
    /// screen never offers either.
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

        if (session.Status != SessionStatus.InProgress)
        {
            throw new TrainingLogException(TrainingLogErrorType.Conflict, CompletionRefusal(session.Status));
        }

        var claimed = await _db.WorkoutSessions
            .Where(candidate => candidate.Id == sessionId && candidate.Status == SessionStatus.InProgress)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(candidate => candidate.Status, SessionStatus.Completed),
                cancellationToken);

        if (claimed == 0)
        {
            // Status se promenio između čitanja i upisa; poruka se čita iz istog mesta kao gore.
            var current = await _db.WorkoutSessions
                .AsNoTracking()
                .Where(candidate => candidate.Id == sessionId)
                .Select(candidate => candidate.Status)
                .FirstAsync(cancellationToken);

            throw new TrainingLogException(TrainingLogErrorType.Conflict, CompletionRefusal(current));
        }

        return session;
    }

    private static string CompletionRefusal(SessionStatus status)
    {
        return status switch
        {
            SessionStatus.Completed => "Workout session is already completed.",
            SessionStatus.Skipped => "A skipped workout cannot be completed.",
            _ => "A workout that has not been started cannot be completed."
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

        var dto = ToDto(
            session,
            weightStepOverrides,
            portions,
            await ResolveEstimatedTargetsAsync(userId, session, portions, weightStepOverrides, cancellationToken),
            isLastTrainingWeek);

        // Beleške uz vežbe čitaju se posebno: vezane su za vežbu i korisnika, a ne za plan.
        var exerciseNotes = await ExerciseNoteResolver.ResolveAsync(
            _db,
            userId,
            session.ExercisePlans.Select(plan => plan.ExerciseId).Distinct().ToList(),
            cancellationToken);
        foreach (var planDto in dto.ExercisePlans)
        {
            planDto.ExerciseNote = exerciseNotes.GetValueOrDefault(planDto.ExerciseId);
        }

        // Samo završen trening ima prozor ispravke; za ostale polje ostaje prazno.
        if (session.Status == SessionStatus.Completed)
        {
            var window = await SessionCorrectionResolver.ResolveAsync(_db, userId, session.Id, cancellationToken);
            dto.Correction = window is null
                ? null
                : new SessionCorrectionDto
                {
                    CanCorrect = window.IsOpen,
                    Mode = window.Mode.ToString(),
                    BlockedBy = window.IsOpen ? null : window.BlockedBy.ToString(),
                    RevisionPending = session.RevisionPending
                };
        }

        return dto;
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
            Note = session.Note,
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
                    ExerciseType = plan.Exercise.Type.ToString(),
                    IsBodyweight = BodyweightPortionResolver.PortionFor(bodyweightPortions, plan.ExerciseId) > 0,
                    BodyweightLoadKg = BodyweightPortionResolver.PortionFor(bodyweightPortions, plan.ExerciseId),
                    IsCalibration = CalibrationSet.AppliesTo(
                        mesocycle.ExperienceLevel,
                        isLastTrainingWeek,
                        plan.Order,
                        plan.Exercise.Type,
                        plan.Exercise.SuitsLowReps,
                        plan.RepRangeMax,
                        plan.TargetRir,
                        mesocycle.Goal),
                    SetLogs = plan.SetLogs
                        .OrderBy(set => set.SetNumber)
                        .Select(SetLogMapper.ToDto)
                        .ToList()
                })
                .ToList()
        };
    }
}
