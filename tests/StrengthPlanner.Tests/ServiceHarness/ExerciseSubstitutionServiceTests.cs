using StrengthPlanner.Application.Exceptions;
using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Domain.Enums;
using StrengthPlanner.Infrastructure.Analytics;
using StrengthPlanner.Infrastructure.Mesocycles;
using StrengthPlanner.Infrastructure.Persistence;
using StrengthPlanner.Infrastructure.TrainingLogs;

namespace StrengthPlanner.Tests.ServiceHarness;

/// <summary>
/// Zamena vežbe za ostatak bloka (A2): isti dan, od izabranog treninga do kraja bloka, samo u
/// treninzima koji čekaju. Prošlost i drugi dani ostaju.
/// </summary>
public class ExerciseSubstitutionServiceTests
{
    private static readonly Guid Quads = Guid.NewGuid();
    private static readonly Guid Hamstrings = Guid.NewGuid();
    private static readonly Guid BackSquat = Guid.NewGuid();
    private static readonly Guid FrontSquat = Guid.NewGuid();
    private static readonly Guid SplitSquat = Guid.NewGuid();
    private static readonly Guid LegCurl = Guid.NewGuid();

    [Fact]
    public async Task TheSwap_ReachesEveryPendingSessionOfTheDay_AndNothingElse()
    {
        using var database = new SqliteDatabase();
        var block = await SeedAsync(database, SetAllocation.TargetVolume);

        await using (var context = database.NewContext())
        {
            var sessionId = await Service(context).SubstituteAsync(database.UserId, block.WeekTwoSquat, FrontSquat);
            Assert.Equal(block.WeekTwoSession, sessionId);
        }

        await using var read = database.NewContext();
        var squatPlans = read.ExercisePlans
            .Where(plan => plan.Order == 1)
            .Select(plan => new
            {
                plan.WorkoutSession.TrainingWeek.WeekNumber,
                plan.WorkoutSession.DayLabel,
                plan.ExerciseId,
                plan.TargetWeightKg
            })
            .ToList()
            .OrderBy(plan => plan.DayLabel).ThenBy(plan => plan.WeekNumber)
            .Select(plan => $"{plan.DayLabel} w{plan.WeekNumber}: {Name(plan.ExerciseId)} {plan.TargetWeightKg?.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) ?? "-"}");

        Assert.Equal(
            [
                "Day A w1: Back Squat 100",
                "Day A w2: Front Squat -",
                "Day A w3: Front Squat -",
                "Day B w2: Back Squat 100",
            ],
            squatPlans);
    }

    [Fact]
    public async Task OnlyTheSameMuscleTypeAndRange_IsOffered_AndNotWhatTheSessionAlreadyHas()
    {
        using var database = new SqliteDatabase();
        var block = await SeedAsync(database, SetAllocation.FollowTemplate);

        await using var context = database.NewContext();
        var alternatives = await Service(context).GetAlternativesAsync(database.UserId, block.WeekTwoSquat);

        // Split Squat ne podnosi opseg snage, Leg Curl je drugi mišić i već je u treningu.
        Assert.Equal(["Front Squat"], alternatives.Select(alternative => alternative.Name));
    }

    [Fact]
    public async Task AnExerciseWithALoggedSet_IsNotSwapped()
    {
        using var database = new SqliteDatabase();
        var block = await SeedAsync(database, SetAllocation.FollowTemplate);

        await using var context = database.NewContext();
        var error = await Assert.ThrowsAsync<TrainingLogException>(() =>
            Service(context).SubstituteAsync(database.UserId, block.WeekOneSquat, FrontSquat));

        Assert.Equal(TrainingLogErrorType.Conflict, error.ErrorType);
    }

    [Fact]
    public async Task AnExerciseTheRuleRejects_IsNotAccepted()
    {
        using var database = new SqliteDatabase();
        var block = await SeedAsync(database, SetAllocation.FollowTemplate);

        await using var context = database.NewContext();
        var error = await Assert.ThrowsAsync<TrainingLogException>(() =>
            Service(context).SubstituteAsync(database.UserId, block.WeekTwoSquat, SplitSquat));

        Assert.Equal(TrainingLogErrorType.Validation, error.ErrorType);
    }

    private static ExerciseSubstitutionService Service(AppDbContext context)
    {
        var landmarks = new VolumeLandmarkService(context);
        return new ExerciseSubstitutionService(
            context,
            new WeeklySetPlanner(context, landmarks, new WeeklyVolumeTargetResolver(context)));
    }

    private static string Name(Guid exerciseId)
    {
        return exerciseId == BackSquat ? "Back Squat" : exerciseId == FrontSquat ? "Front Squat" : exerciseId.ToString();
    }

    private sealed record Block(Guid WeekOneSquat, Guid WeekTwoSquat, Guid WeekTwoSession);

    /// <summary>
    /// Blok od tri nedelje, Day A: čučanj pa Leg Curl. Prva nedelja je odrađena (čučanj ima
    /// seriju), druga i treća čekaju. Day B druge nedelje ima isti čučanj i ne sme da se menja.
    /// </summary>
    private static async Task<Block> SeedAsync(SqliteDatabase database, SetAllocation allocation)
    {
        await using var db = database.NewContext();
        db.MuscleGroups.AddRange(
            new MuscleGroup { Id = Quads, Name = "Quads" },
            new MuscleGroup { Id = Hamstrings, Name = "Hamstrings" });
        db.VolumeLandmarks.AddRange(
            new VolumeLandmark { Id = Guid.NewGuid(), MuscleGroupId = Quads, Mev = 8, Mav = 14, Mrv = 20 },
            new VolumeLandmark { Id = Guid.NewGuid(), MuscleGroupId = Hamstrings, Mev = 6, Mav = 11, Mrv = 16 });
        db.Exercises.AddRange(
            Exercise(BackSquat, "Back Squat", ExerciseType.Compound, suitsLowReps: true, Quads),
            Exercise(FrontSquat, "Front Squat", ExerciseType.Compound, suitsLowReps: true, Quads),
            Exercise(SplitSquat, "Split Squat", ExerciseType.Compound, suitsLowReps: false, Quads),
            Exercise(LegCurl, "Leg Curl", ExerciseType.Isolation, suitsLowReps: true, Hamstrings));

        var mesocycle = new Mesocycle
        {
            Id = Guid.NewGuid(),
            UserId = database.UserId,
            Name = "Blok",
            Goal = Goal.Strength,
            ExperienceLevel = ExperienceLevel.Intermediate,
            SetAllocation = allocation,
            StartDate = new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc),
            IsActive = true
        };

        Guid weekOneSquat = Guid.Empty, weekTwoSquat = Guid.Empty, weekTwoSession = Guid.Empty;
        for (var weekNumber = 1; weekNumber <= 3; weekNumber++)
        {
            var week = new TrainingWeek { Id = Guid.NewGuid(), WeekNumber = weekNumber };
            mesocycle.Weeks.Add(week);

            var dayA = new WorkoutSession
            {
                Id = Guid.NewGuid(),
                DayLabel = "Day A",
                Status = weekNumber == 1 ? SessionStatus.Completed : SessionStatus.Planned
            };
            var squat = Plan(BackSquat, 1);
            dayA.ExercisePlans.Add(squat);
            dayA.ExercisePlans.Add(Plan(LegCurl, 2));
            week.Sessions.Add(dayA);

            if (weekNumber == 1)
            {
                squat.SetLogs.Add(new SetLog
                {
                    Id = Guid.NewGuid(),
                    SetNumber = 1,
                    WeightKg = 100m,
                    Reps = 5,
                    Rir = 2,
                    PerformedAt = DateTime.UtcNow
                });
                weekOneSquat = squat.Id;
            }

            if (weekNumber == 2)
            {
                weekTwoSquat = squat.Id;
                weekTwoSession = dayA.Id;
                var dayB = new WorkoutSession { Id = Guid.NewGuid(), DayLabel = "Day B", Status = SessionStatus.Planned };
                dayB.ExercisePlans.Add(Plan(BackSquat, 1));
                week.Sessions.Add(dayB);
            }
        }

        db.Mesocycles.Add(mesocycle);
        await db.SaveChangesAsync();
        return new Block(weekOneSquat, weekTwoSquat, weekTwoSession);
    }

    private static Exercise Exercise(Guid id, string name, ExerciseType type, bool suitsLowReps, Guid primary)
    {
        return new Exercise
        {
            Id = id,
            Name = name,
            Type = type,
            Equipment = "Barbell",
            WeightStepKg = 2.5m,
            SuitsLowReps = suitsLowReps,
            Muscles = [new ExerciseMuscle { MuscleGroupId = primary, Contribution = 1.0m }]
        };
    }

    private static ExercisePlan Plan(Guid exerciseId, int order)
    {
        return new ExercisePlan
        {
            Id = Guid.NewGuid(),
            ExerciseId = exerciseId,
            Order = order,
            TargetSets = 3,
            PrescribedSets = 3,
            RepRangeMin = 3,
            RepRangeMax = 6,
            BaseRepRangeMin = 3,
            BaseRepRangeMax = 6,
            TargetRir = 2,
            TargetWeightKg = 100m
        };
    }
}
