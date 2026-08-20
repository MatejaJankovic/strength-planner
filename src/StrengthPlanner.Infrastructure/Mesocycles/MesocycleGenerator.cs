using Microsoft.EntityFrameworkCore;
using StrengthPlanner.Application.DTOs.Mesocycles;
using StrengthPlanner.Application.Exceptions;
using StrengthPlanner.Application.Interfaces;
using StrengthPlanner.Application.Templates;
using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Domain.Enums;
using StrengthPlanner.Infrastructure.Exercises;
using StrengthPlanner.Infrastructure.Persistence;
using StrengthPlanner.Infrastructure.Templates;

namespace StrengthPlanner.Infrastructure.Mesocycles;

public class MesocycleGenerator : IMesocycleGenerator
{
    private readonly AppDbContext _db;
    private readonly WeeklySetPlanner _setPlanner;
    private readonly IWorkoutTemplateResolver _templateResolver;
    private readonly E1RmCalculator _e1RmCalculator = new();

    public MesocycleGenerator(
        AppDbContext db,
        WeeklySetPlanner setPlanner,
        IWorkoutTemplateResolver templateResolver)
    {
        _db = db;
        _setPlanner = setPlanner;
        _templateResolver = templateResolver;
    }

    public async Task<MesocycleDto> GenerateAsync(
        Guid userId,
        GenerateMesocycleRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var name = request.Name.Trim();
        if (name.Length == 0)
        {
            throw new MesocycleGenerationException("Mesocycle name is required.");
        }

        ResolvedTemplate? template;
        try
        {
            template = await _templateResolver.ResolveAsync(userId, request.TemplateKey, cancellationToken);
        }
        catch (MissingTemplateExercisesException exception)
        {
            throw new MesocycleGenerationException(exception.Message);
        }

        if (template is null)
        {
            throw new MesocycleGenerationException($"Unknown workout template: '{request.TemplateKey}'.");
        }

        var goalSettings = GoalPrescriptions.ForGoal(request.Goal);

        // Nivo iskustva određuje koliko serija vežba nosi na startu. Koliko vežbi trening
        // nosi, odlučeno je već u razrešavanju šablona - lični šablon se ne skraćuje.
        var experienceLevel = await _db.Profiles
            .AsNoTracking()
            .Where(profile => profile.UserId == userId)
            .Select(profile => (ExperienceLevel?)profile.ExperienceLevel)
            .FirstOrDefaultAsync(cancellationToken) ?? ExperienceLevel.Intermediate;

        var exercises = template.Days
            .SelectMany(day => day.Exercises)
            .Select(planned => planned.Exercise)
            .DistinctBy(exercise => exercise.Id)
            .ToList();

        var exerciseIds = exercises.Select(exercise => exercise.Id).ToList();
        var weightStepByExerciseId = await WeightStepResolver.ResolveAsync(
            _db,
            userId,
            exerciseIds,
            cancellationToken);
        var oneRepMaxRecords = await _db.OneRepMaxRecords
            .AsNoTracking()
            .Where(record => record.UserId == userId && exerciseIds.Contains(record.ExerciseId))
            .OrderByDescending(record => record.RecordedAt)
            .ThenByDescending(record => record.Id)
            .ToListAsync(cancellationToken);

        // Najbolji 1RM u skorašnjem prozoru, ne najnoviji: poslednji zapis može
        // biti sa slabijeg dana pa bi novi ciklus krenuo preblago. Ako u prozoru
        // nema ničega, uzmi najnoviji zapis ikada.
        var lookbackCutoff = DateTime.UtcNow.AddDays(-TrainingConstants.OneRepMaxLookbackDays);
        var oneRepMaxByExerciseId = oneRepMaxRecords
            .GroupBy(record => record.ExerciseId)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var recent = group.Where(record => record.RecordedAt >= lookbackCutoff).ToList();
                    return recent.Count > 0
                        ? recent.Max(record => record.ValueKg)
                        : group.First().ValueKg;
                });

        // Kada generator radi unutar već otvorene transakcije (npr. pri automatskom
        // prelasku na sledeći blok dugoročnog plana), ne otvara svoju — inače bi
        // ugnježdena transakcija pukla, a i prelazak mora da deli sudbinu sa
        // završetkom treninga koji ga je pokrenuo.
        // await using i na null-u je legalan no-op, pa se transakcija oslobadja i kada
        // se izadje izuzetkom.
        await using var transaction = _db.Database.CurrentTransaction is null
            ? await _db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var activeMesocycles = await _db.Mesocycles
            .Where(mesocycle => mesocycle.UserId == userId && mesocycle.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var activeMesocycle in activeMesocycles)
        {
            activeMesocycle.IsActive = false;
        }

        var startDate = DateTime.SpecifyKind(request.StartDate.Date, DateTimeKind.Utc);
        var mesocycle = BuildMesocycle(
            userId,
            name,
            request.Goal,
            startDate,
            template,
            goalSettings,
            oneRepMaxByExerciseId,
            weightStepByExerciseId,
            experienceLevel,
            request.PeriodizationModel,
            request.SetAllocation);

        _db.Mesocycles.Add(mesocycle);
        await _db.SaveChangesAsync(cancellationToken);

        // Propis daje svakoj vežbi isti broj serija, pa nedeljni volumen po mišiću ispadne
        // onako kako se šablon slučajno sabere. Balansiranje preraspoređuje serije tako da
        // nedelja padne u ciljnu zonu svakog mišića koji trenira. Radi nad upravo upisanim
        // (praćenim) planovima, pa je i DTO ispod već izbalansiran.
        //
        // Preskače se kada je blok izabrao da prati šablon doslovno. Broj serija koji je
        // korisnik otkucao je tada namera, a ne predlog: pre ovog izbora se uneto 3 tiho
        // pretvaralo u 5, bez ijedne poruke o tome zašto.
        if (mesocycle.SetAllocation == SetAllocation.TargetVolume)
        {
            await _setPlanner.RebalanceAsync(userId, mesocycle.Id, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
        }

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        var exerciseNameById = exercises.ToDictionary(exercise => exercise.Id, exercise => exercise.Name);
        return ToDto(mesocycle, exerciseNameById, weightStepByExerciseId);
    }

    private Mesocycle BuildMesocycle(
        Guid userId,
        string name,
        Goal goal,
        DateTime startDate,
        ResolvedTemplate template,
        GoalPrescription goalSettings,
        IReadOnlyDictionary<Guid, decimal> oneRepMaxByExerciseId,
        IReadOnlyDictionary<Guid, decimal> weightStepByExerciseId,
        ExperienceLevel experienceLevel,
        PeriodizationModel periodizationModel,
        SetAllocation setAllocation)
    {
        var startingSets = ExperienceProgramming.StartingSetsPerExercise(experienceLevel);

        // Model određuje i koliko blok traje i kako se propis menja iz nedelje u nedelju.
        var prescriptions = Periodization.ForBlock(
            periodizationModel,
            goalSettings.RepRangeMin,
            goalSettings.RepRangeMax,
            goalSettings.TargetRir,
            startingSets);

        var mesocycle = new Mesocycle
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            Goal = goal,
            PeriodizationModel = periodizationModel,
            SetAllocation = setAllocation,
            StartDate = startDate,
            DurationWeeks = prescriptions.Count,
            IsActive = true
        };

        foreach (var prescription in prescriptions)
        {
            var weekNumber = prescription.WeekNumber;
            var week = new TrainingWeek
            {
                Id = Guid.NewGuid(),
                WeekNumber = weekNumber,
                IsDeload = prescription.IsDeload
            };

            for (var dayIndex = 0; dayIndex < template.Days.Count; dayIndex++)
            {
                var templateDay = template.Days[dayIndex];
                var session = new WorkoutSession
                {
                    Id = Guid.NewGuid(),
                    DayLabel = templateDay.Name,
                    Date = GetSessionDate(startDate, weekNumber, dayIndex, template.Days.Count),
                    Status = SessionStatus.Planned
                };

                var dayExercises = templateDay.Exercises;

                for (var exerciseIndex = 0; exerciseIndex < dayExercises.Count; exerciseIndex++)
                {
                    var planned = dayExercises[exerciseIndex];
                    var exercise = planned.Exercise;

                    // Vežba iz ličnog šablona nosi svoje serije i opseg; ista periodizacija
                    // ih pomera kroz blok kao što pomera propis izveden iz cilja, jer
                    // ForWeek i inače prima "osnovu" kao parametar. RIR ostaje iz cilja.
                    var baseRepRangeMin = planned.RepRangeMin ?? goalSettings.RepRangeMin;
                    var baseRepRangeMax = planned.RepRangeMax ?? goalSettings.RepRangeMax;

                    var exercisePrescription = planned.Sets is null
                        ? prescription
                        : Periodization.ForWeek(
                            periodizationModel,
                            weekNumber,
                            baseRepRangeMin,
                            baseRepRangeMax,
                            goalSettings.TargetRir,
                            planned.Sets.Value);

                    var targetWeightKg = GetInitialTargetWeight(
                        weekNumber,
                        exercise.Id,
                        exercisePrescription,
                        oneRepMaxByExerciseId,
                        WeightStepResolver.StepFor(weightStepByExerciseId, exercise.Id));

                    session.ExercisePlans.Add(new ExercisePlan
                    {
                        Id = Guid.NewGuid(),
                        ExerciseId = exercise.Id,
                        Order = exerciseIndex + 1,
                        // Propis nedelje je polazna vrednost i za predlog i za sidro;
                        // balansiranje volumena ispod pomera samo predlog.
                        TargetSets = exercisePrescription.Sets,
                        PrescribedSets = exercisePrescription.Sets,
                        RepRangeMin = exercisePrescription.RepRangeMin,
                        RepRangeMax = exercisePrescription.RepRangeMax,
                        BaseRepRangeMin = baseRepRangeMin,
                        BaseRepRangeMax = baseRepRangeMax,
                        TargetRir = exercisePrescription.TargetRir,
                        TargetWeightKg = targetWeightKg
                    });
                }

                week.Sessions.Add(session);
            }

            mesocycle.Weeks.Add(week);
        }

        return mesocycle;
    }

    /// <summary>
    /// Opterećenje za prvu nedelju, izvedeno iz poznatog 1RM-a i propisa te nedelje.
    /// Kasnije nedelje puni progresija kada se prethodna završi — tek tada se zna šta je
    /// korisnik zaista uradio.
    /// </summary>
    private decimal? GetInitialTargetWeight(
        int weekNumber,
        Guid exerciseId,
        WeekPrescription prescription,
        IReadOnlyDictionary<Guid, decimal> oneRepMaxByExerciseId,
        decimal weightStepKg)
    {
        if (weekNumber != 1 || !oneRepMaxByExerciseId.TryGetValue(exerciseId, out var oneRepMax))
        {
            return null;
        }

        return _e1RmCalculator.WorkingWeightFor(
            oneRepMax,
            prescription.RepRangeMin,
            prescription.TargetRir,
            weightStepKg);
    }

    private static DateTime GetSessionDate(DateTime startDate, int weekNumber, int dayIndex, int daysPerWeek)
    {
        var weekStartDate = startDate.AddDays((weekNumber - 1) * 7);

        return weekStartDate.AddDays(TrainingWeekSchedule.OffsetFor(daysPerWeek, dayIndex));
    }

    private static MesocycleDto ToDto(
        Mesocycle mesocycle,
        IReadOnlyDictionary<Guid, string> exerciseNameById,
        IReadOnlyDictionary<Guid, decimal> weightStepByExerciseId)
    {
        return new MesocycleDto
        {
            Id = mesocycle.Id,
            Name = mesocycle.Name,
            Goal = mesocycle.Goal,
            StartDate = mesocycle.StartDate,
            DurationWeeks = mesocycle.DurationWeeks,
            PeriodizationModel = mesocycle.PeriodizationModel,
            IsActive = mesocycle.IsActive,
            Weeks = mesocycle.Weeks
                .OrderBy(week => week.WeekNumber)
                .Select(week => new TrainingWeekDto
                {
                    Id = week.Id,
                    WeekNumber = week.WeekNumber,
                    IsDeload = week.IsDeload,
                    IsAutoDeload = week.IsAutoDeload,
                    FatigueScore = week.FatigueScore,
                    Sessions = week.Sessions
                        .OrderBy(session => session.Date)
                        .Select(session => new WorkoutSessionDto
                        {
                            Id = session.Id,
                            WeekNumber = week.WeekNumber,
                            IsDeload = week.IsDeload,
                            IsAutoDeload = week.IsAutoDeload,
                            DayLabel = session.DayLabel,
                            Date = session.Date,
                            Status = session.Status.ToString(),
                            ExercisePlans = session.ExercisePlans
                                .OrderBy(plan => plan.Order)
                                .Select(plan => new ExercisePlanDto
                                {
                                    Id = plan.Id,
                                    ExerciseId = plan.ExerciseId,
                                    ExerciseName = exerciseNameById.GetValueOrDefault(plan.ExerciseId, string.Empty),
                                    Order = plan.Order,
                                    TargetSets = plan.TargetSets,
                                    PrescribedSets = plan.PrescribedSets,
                                    RepRangeMin = plan.RepRangeMin,
                                    RepRangeMax = plan.RepRangeMax,
                                    TargetRir = plan.TargetRir,
                                    TargetWeightKg = plan.TargetWeightKg,
                                    WeightStepKg = WeightStepResolver.StepFor(
                                        weightStepByExerciseId,
                                        plan.ExerciseId)
                                })
                                .ToList()
                        })
                        .ToList()
                })
                .ToList()
        };
    }

}
