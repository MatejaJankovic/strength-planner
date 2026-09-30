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
    private readonly E1RmCalculator _e1RmCalculator = new();
    private readonly ProgressionEngine _progressionEngine = new();

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

        // Provera iznad je samo brzi izlaz: čitanje bez zaključavanja ne sprečava da dva
        // istovremena zahteva oba prođu. Uslovni UPDATE preuzima sesiju atomično — drugi
        // zahtev čeka na redu i dobija nula redova, pa se progresija, e1RM zapisi i
        // granice volumena obračunavaju tačno jednom.
        var claimed = await _db.WorkoutSessions
            .Where(candidate => candidate.Id == sessionId && candidate.Status != SessionStatus.Completed)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(candidate => candidate.Status, SessionStatus.Completed),
                cancellationToken);

        if (claimed == 0)
        {
            throw new TrainingLogException(TrainingLogErrorType.Conflict, "Workout session is already completed.");
        }

        var exerciseIds = session.ExercisePlans
            .Select(plan => plan.ExerciseId)
            .Distinct()
            .ToList();
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
        var previousMaxByExerciseId = await _db.OneRepMaxRecords
            .AsNoTracking()
            .Where(record => record.UserId == userId && exerciseIds.Contains(record.ExerciseId))
            .GroupBy(record => record.ExerciseId)
            .Select(group => new { ExerciseId = group.Key, ValueKg = group.Max(record => record.ValueKg) })
            .ToDictionaryAsync(record => record.ExerciseId, record => record.ValueKg, cancellationToken);

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

        // Samo sesije koje još nisu završene: complete van redosleda ne sme da
        // prepiše ciljeve već odrađenih treninga.
        var nextPlans = await _db.ExercisePlans
            .Include(plan => plan.WorkoutSession)
                .ThenInclude(workoutSession => workoutSession.TrainingWeek)
            .Where(plan => plan.WorkoutSession.TrainingWeek.MesocycleId == session.TrainingWeek.MesocycleId
                           && plan.WorkoutSession.DayLabel == session.DayLabel
                           && plan.WorkoutSession.TrainingWeek.WeekNumber > session.TrainingWeek.WeekNumber
                           && plan.WorkoutSession.Status != SessionStatus.Completed
                           && exerciseIds.Contains(plan.ExerciseId))
            .OrderBy(plan => plan.WorkoutSession.TrainingWeek.WeekNumber)
            .ThenBy(plan => plan.Order)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        // Status je već upisan uslovnim UPDATE-om; ovo samo usklađuje učitanu instancu
        // sa bazom da bi DTO ispod prijavio tačno stanje.
        session.Status = SessionStatus.Completed;

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
            var load = WorkingLoad.Select(logs.Select(ToLoggedSet).ToList());

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
                    RecordedAt = now
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
            var currentPrescription = PrescriptionOf(plan);
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
                    PrescriptionOf(nextPlan),
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

        await _db.SaveChangesAsync(cancellationToken);

        // Granice volumena uče iz svake nedelje koja je u celosti odrađena a još nije
        // obračunata; sama metoda uslovnim UPDATE-om obezbeđuje da se nedelja obračuna
        // tačno jednom. Ostaje u istoj transakciji kao i završetak sesije, pa se ili
        // upiše sve ili ništa.
        await _volumeLandmarks.AdaptPendingWeeksAsync(
            userId,
            session.TrainingWeek.MesocycleId,
            now,
            cancellationToken);

        // Procena umora ide POSLE progresije: deload prepisuje opterećenja koja je
        // progresija upravo popunila za narednu nedelju.
        var autoDeload = await _deloads.EvaluatePendingWeeksAsync(
            userId,
            session.TrainingWeek.MesocycleId,
            cancellationToken);

        // Rezime se zaključuje TEK ovde: sve do ovog trenutka je moglo da prepiše težinu
        // naredne nedelje — progresija, pravilo za narednu nedelju, pa i auto-deload.
        FinalizeSummaries(summaries, nextPlans);

        // Sve što se tiče samog treninga mora da bude upisano pre prelaska na sledeći
        // blok — generator ispod poziva svoj SaveChanges, pa se na njega ne oslanjamo.
        await _db.SaveChangesAsync(cancellationToken);

        // Predlog serija se preračunava POSLE deload-a, i to tek pošto je deload UPISAN.
        // Oba dela ovog redosleda nose težinu:
        //
        // Posle deload-a, jer rasterećenje menja propis od koga balansiranje polazi.
        // Posle upisa, jer balansiranje pita bazu koje su nedelje deload — a DeloadService
        // pretvaranje nedelje ostavlja u change trackeru. Bez SaveChanges iznad, upit i
        // dalje vidi staro stanje, sveže rasterećenu nedelju uzima kao običnu i vraća joj
        // prepolovljene serije na četiri: korisnik dobije „deload" sa volumenom pune
        // trenažne nedelje. Izmereno pre ispravke — cela nedelja se vratila sa dve na
        // četiri serije po vežbi.
        //
        // Ovim treningom je deo nedeljnog volumena upisan (ili propušten), pa treninzi koji
        // u toj nedelji tek predstoje dobijaju predlog koji nedelju vraća u ciljnu zonu.
        // Blok koji prati šablon doslovno se ne balansira ni ovde. Da se preskakalo samo
        // pri generisanju, prvi završen trening bi vratio serije na ciljni volumen — pa bi
        // izbor važio tačno do prvog treninga.
        var volumeAdjustments =
            session.TrainingWeek.Mesocycle.SetAllocation == SetAllocation.TargetVolume
                ? await _setPlanner.RebalanceAsync(
                    userId,
                    session.TrainingWeek.MesocycleId,
                    cancellationToken)
                : [];

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

        return new CompleteSessionResultDto
        {
            SessionId = session.Id,
            Status = session.Status.ToString(),
            Exercises = summaries,
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
    private static void FinalizeSummaries(
        List<CompletedExerciseSummaryDto> summaries,
        IReadOnlyList<ExercisePlan> nextPlans)
    {
        foreach (var summary in summaries)
        {
            var nextPlan = nextPlans.FirstOrDefault(plan => plan.ExerciseId == summary.ExerciseId);

            summary.NextWeightKg = nextPlan?.TargetWeightKg;
            summary.WeightChangeKg = NextWeekLoad.ChangeKg(summary.UsedWeightKg, summary.NextWeightKg);
            summary.WeightIncreased = summary.WeightChangeKg > 0;
        }
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
                .Select(ToLoggedSet)
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
                PrescriptionOf(plan),
                load.ReferenceWeightKg,
                progression.NextWeightKg);
        }

        return resumePoints;
    }

    private static LoadPrescription PrescriptionOf(ExercisePlan plan)
    {
        return new LoadPrescription(plan.RepRangeMin, plan.RepRangeMax, plan.TargetRir);
    }

    private static LoggedSet ToLoggedSet(SetLog set)
    {
        return new LoggedSet(set.WeightKg, set.Reps, set.Rir, set.IsFailure, set.BodyweightLoadKg);
    }

    /// <summary>Where progression stood before a deload week interrupted it.</summary>
    private sealed record ResumePoint(
        LoadPrescription Prescription,
        decimal ReferenceWeightKg,
        decimal ProgressionWeightKg);

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

        return ToDto(
            session,
            weightStepOverrides,
            portions,
            await ResolveEstimatedTargetsAsync(userId, session, portions, weightStepOverrides, cancellationToken));
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
    /// </summary>
    private async Task<Dictionary<Guid, decimal>> ResolveEstimatedTargetsAsync(
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

        var estimates = new Dictionary<Guid, decimal>();

        foreach (var plan in missing)
        {
            var estimate = StartingLoad.FromOneRepMax(
                _e1RmCalculator,
                oneRepMaxByExerciseId.TryGetValue(plan.ExerciseId, out var oneRepMax) ? oneRepMax : null,
                plan.RepRangeMin,
                plan.TargetRir,
                BodyweightPortionResolver.PortionFor(bodyweightPortions, plan.ExerciseId),
                WeightStepResolver.Effective(weightStepOverrides, plan.ExerciseId, plan.Exercise.WeightStepKg));

            if (estimate is not null)
            {
                estimates[plan.Id] = estimate.Value;
            }
        }

        return estimates;
    }

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
        return _e1RmCalculator.BestEstimate(logs.Select(ToLoggedSet));
    }

    private static WorkoutSessionDto ToDto(
        WorkoutSession session,
        IReadOnlyDictionary<Guid, decimal> weightStepOverrides,
        IReadOnlyDictionary<Guid, decimal> bodyweightPortions,
        IReadOnlyDictionary<Guid, decimal> estimatedTargets)
    {
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
                                         ? estimate
                                         : null),
                    // Razlika koju vežbač treba da vidi: cilj izveden iz maksimuma nije isto
                    // što i cilj koji je progresija izvela iz odrađenih serija.
                    TargetWeightIsEstimate = plan.TargetWeightKg is null
                                             && estimatedTargets.ContainsKey(plan.Id),
                    WeightStepKg = WeightStepResolver.Effective(
                        weightStepOverrides,
                        plan.ExerciseId,
                        plan.Exercise.WeightStepKg),
                    Equipment = plan.Exercise.Equipment,
                    IsBodyweight = BodyweightPortionResolver.PortionFor(bodyweightPortions, plan.ExerciseId) > 0,
                    BodyweightLoadKg = BodyweightPortionResolver.PortionFor(bodyweightPortions, plan.ExerciseId),
                    SetLogs = plan.SetLogs
                        .OrderBy(set => set.SetNumber)
                        .Select(SetLogMapper.ToDto)
                        .ToList()
                })
                .ToList()
        };
    }
}
