using Microsoft.EntityFrameworkCore;
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

        var weightStepOverrides = await WeightStepResolver.LoadOverridesAsync(_db, userId, cancellationToken);
        return ToDto(session, weightStepOverrides);
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

        if (session.Status == SessionStatus.Planned)
        {
            session.Status = SessionStatus.InProgress;
            await _db.SaveChangesAsync(cancellationToken);
        }

        var detailedSession = await BuildSessionDetailsQuery(userId)
            .FirstAsync(workoutSession => workoutSession.Id == sessionId, cancellationToken);

        var weightStepOverrides = await WeightStepResolver.LoadOverridesAsync(_db, userId, cancellationToken);
        return ToDto(detailedSession, weightStepOverrides);
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
        var previousMaxByExerciseId = await _db.OneRepMaxRecords
            .AsNoTracking()
            .Where(record => record.UserId == userId && exerciseIds.Contains(record.ExerciseId))
            .GroupBy(record => record.ExerciseId)
            .Select(group => new { ExerciseId = group.Key, ValueKg = group.Max(record => record.ValueKg) })
            .ToDictionaryAsync(record => record.ExerciseId, record => record.ValueKg, cancellationToken);

        // Za preračun opterećenja se gleda samo skorašnji prozor, isto kao pri generisanju
        // bloka. Rekord od pre pola godine je istorija, a ne procena trenutne snage — a
        // ovde bi postao ciljno opterećenje naredne nedelje.
        var recentCutoff = DateTime.UtcNow.AddDays(-TrainingConstants.OneRepMaxLookbackDays);
        var recentMaxByExerciseId = await _db.OneRepMaxRecords
            .AsNoTracking()
            .Where(record => record.UserId == userId
                             && exerciseIds.Contains(record.ExerciseId)
                             && record.RecordedAt >= recentCutoff)
            .GroupBy(record => record.ExerciseId)
            .Select(group => new { ExerciseId = group.Key, ValueKg = group.Max(record => record.ValueKg) })
            .ToDictionaryAsync(record => record.ExerciseId, record => record.ValueKg, cancellationToken);

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

            if (logs.Count == 0)
            {
                if (nextPlan is not null)
                {
                    nextPlan.TargetWeightKg = plan.TargetWeightKg;
                    summary.NextWeightKg = nextPlan.TargetWeightKg;
                }

                summaries.Add(summary);
                continue;
            }

            // Deload serije su namerno submaksimalne — njihov e1RM bi veštački
            // oborio trend snage i start sledećeg mezociklusa, pa se ne upisuje.
            var bestEstimate = session.TrainingWeek.IsDeload ? null : EstimateBestOneRepMax(logs);
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

            // Progresija polazi od težine koju je korisnik STVARNO koristio;
            // planska težina je samo fallback (logova ovde uvek ima).
            var usedWeight = logs.Count > 0
                ? logs.Average(set => set.WeightKg)
                : plan.TargetWeightKg ?? 0m;
            var workingSets = logs
                .Select(set => new WorkingSet(set.Reps, set.Rir, set.IsFailure))
                .ToList();
            var weightStepKg = WeightStepResolver.StepFor(weightStepByExerciseId, plan.ExerciseId);
            var progression = _progressionEngine.ComputeNext(
                usedWeight,
                workingSets,
                plan.TargetRir,
                plan.RepRangeMin,
                plan.RepRangeMax,
                weightStepKg);

            summary.NextWeightKg = progression.NextWeightKg;
            summary.WeightIncreased = progression.WeightIncreased;

            if (nextPlan is not null)
            {
                recentMaxByExerciseId.TryGetValue(plan.ExerciseId, out var recentMax);
                var nextWeight = NextTargetWeight(
                    plan,
                    nextPlan,
                    progression.NextWeightKg,
                    usedWeight,
                    bestEstimate ?? (recentMax > 0 ? recentMax : null),
                    weightStepKg);

                nextPlan.TargetWeightKg = nextWeight;
                summary.NextWeightKg = nextWeight;
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

        if (autoDeload is not null)
        {
            RefreshSummariesAfterDeload(summaries, nextPlans);
        }

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
        var volumeAdjustments = await _setPlanner.RebalanceAsync(
            userId,
            session.TrainingWeek.MesocycleId,
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
        MacrocycleAdvance? nextBlock = null;
        const string advanceSavepoint = "before_block_advance";
        await transaction.CreateSavepointAsync(advanceSavepoint, cancellationToken);

        try
        {
            nextBlock = await _macrocycles.AdvanceIfFinishedAsync(
                userId,
                session.TrainingWeek.MesocycleId,
                now,
                cancellationToken);

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.ReleaseSavepointAsync(advanceSavepoint, cancellationToken);
        }
        catch (MesocycleGenerationException)
        {
            // Povratak na savepoint, a ne samo hvatanje izuzetka: da je pukla neka SQL
            // naredba, cela transakcija bi u PostgreSQL-u bila u prekinutom stanju i
            // commit ispod bi svejedno pao.
            await transaction.RollbackToSavepointAsync(advanceSavepoint, cancellationToken);
            nextBlock = null;
        }

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
                    Muscle = adjustment.Muscle
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
    /// Rezime treninga je popunjen tokom progresije, a deload posle toga prepisuje ista
    /// (praćena) planska zaduženja. Bez ovog usklađivanja korisnik bi u istom ekranu
    /// video poruku "nedelja je pretvorena u deload" i, ispod nje, uvećanu težinu koju
    /// je progresija predložila pre te odluke.
    /// </summary>
    private static void RefreshSummariesAfterDeload(
        List<CompletedExerciseSummaryDto> summaries,
        IReadOnlyList<ExercisePlan> nextPlans)
    {
        foreach (var summary in summaries)
        {
            var nextPlan = nextPlans.FirstOrDefault(plan => plan.ExerciseId == summary.ExerciseId);
            if (nextPlan is null)
            {
                continue;
            }

            summary.NextWeightKg = nextPlan.TargetWeightKg;
            summary.WeightIncreased = false;
        }
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
    /// Opterećenje za istu vežbu u narednoj nedelji.
    ///
    /// Tri slučaja, i razlikuju se suštinski:
    ///
    /// <list type="bullet">
    /// <item><b>Deload</b> — 90% <i>stvarno</i> korišćene težine, bez progresije.</item>
    /// <item><b>Naredna nedelja traži drugačiji propis</b> (periodizacija) — nošenje iste
    /// težine nema smisla: nedelja koja pada sa 10 na 5 ponavljanja mora da bude teža, a ne
    /// ista uvećana za jedan korak. Opterećenje se izvodi iz najsvežije procene 1RM-a i
    /// propisa te nedelje, isto kao pri generisanju prve nedelje.</item>
    /// <item><b>Isti propis</b> — obična dupla progresija, ponašanje nepromenjeno.</item>
    /// </list>
    ///
    /// Ako skorašnje procene 1RM-a nema — nijedna serija nije upisana, ili su sve bile
    /// iznad Epley granice pa se e1RM ne beleži — ostaje progresija: pogrešnija, ali bolja
    /// od opterećenja izvedenog iz rekorda starog nekoliko meseci.
    /// </summary>
    private decimal NextTargetWeight(
        ExercisePlan plan,
        ExercisePlan nextPlan,
        decimal progressionWeightKg,
        decimal usedWeightKg,
        decimal? oneRepMaxKg,
        decimal weightStepKg)
    {
        if (nextPlan.WorkoutSession.TrainingWeek.IsDeload)
        {
            return WeightMath.RoundToStep(usedWeightKg * TrainingConstants.DeloadWeightFactor, weightStepKg);
        }

        var samePrescription = nextPlan.RepRangeMin == plan.RepRangeMin
                               && nextPlan.RepRangeMax == plan.RepRangeMax
                               && nextPlan.TargetRir == plan.TargetRir;

        if (samePrescription || oneRepMaxKg is null)
        {
            return progressionWeightKg;
        }

        return _e1RmCalculator.WorkingWeightFor(
            oneRepMaxKg.Value,
            nextPlan.RepRangeMin,
            nextPlan.TargetRir,
            weightStepKg);
    }

    private decimal? EstimateBestOneRepMax(IReadOnlyList<SetLog> logs)
    {
        decimal? bestEstimate = null;

        // Namerno upisani Rir, ne WorkingSet.EffectiveRir: Epley ionako pretpostavlja
        // seriju do otkaza, pa je za otkaz tačna vrednost 0. Efektivni RIR ume da bude
        // negativan i služi isključivo auto-regulaciji — ovde bi oborio procenu i pukao
        // na proveri u E1RmCalculator.
        foreach (var log in logs.Where(log => log.Reps <= TrainingConstants.EpleyRepCap))
        {
            var estimate = _e1RmCalculator.EstimateOneRepMax(log.WeightKg, log.Reps, log.Rir);
            if (!bestEstimate.HasValue || estimate > bestEstimate.Value)
            {
                bestEstimate = estimate;
            }
        }

        return bestEstimate;
    }

    private static WorkoutSessionDto ToDto(
        WorkoutSession session,
        IReadOnlyDictionary<Guid, decimal> weightStepOverrides)
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
                    TargetWeightKg = plan.TargetWeightKg,
                    WeightStepKg = WeightStepResolver.Effective(
                        weightStepOverrides,
                        plan.ExerciseId,
                        plan.Exercise.WeightStepKg),
                    SetLogs = plan.SetLogs
                        .OrderBy(set => set.SetNumber)
                        .Select(set => new SetLogDto
                        {
                            Id = set.Id,
                            ExercisePlanId = set.ExercisePlanId,
                            SetNumber = set.SetNumber,
                            WeightKg = set.WeightKg,
                            Reps = set.Reps,
                            Rir = set.Rir,
                            IsFailure = set.IsFailure,
                            PerformedAt = set.PerformedAt
                        })
                        .ToList()
                })
                .ToList()
        };
    }
}
