using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Domain.Enums;
using StrengthPlanner.Infrastructure.Analytics;
using StrengthPlanner.Infrastructure.TrainingLogs;

namespace StrengthPlanner.Tests.ServiceHarness;

/// <summary>
/// Deload polovi polazni broj serija bloka, a polazni broj se čita iz osnovne nedelje (runde
/// 10 i 14), jer izvođenje greši za redove starijih pravila i kod donje granice serija. Vežba
/// zamenjena posle osnovne nedelje (A2) u njoj ne postoji, pa je deload padao na izvođenje
/// (pregled #111). Sada se mesto traži po rednom broju u danu.
/// </summary>
public class SwappedExerciseDeloadTests
{
    [Fact]
    public async Task AnAutoDeload_ReadsTheBaseOfASwappedSlot_FromTheBaseWeek()
    {
        using var database = new SqliteDatabase();
        var backSquat = Guid.NewGuid();
        var frontSquat = Guid.NewGuid();
        Guid mesocycleId;

        await using (var db = database.NewContext())
        {
            var quads = Guid.NewGuid();
            db.MuscleGroups.Add(new MuscleGroup { Id = quads, Name = "Quads" });
            db.Exercises.AddRange(
                Exercise(backSquat, "Back Squat", quads),
                Exercise(frontSquat, "Front Squat", quads));

            var mesocycle = new Mesocycle
            {
                Id = Guid.NewGuid(),
                UserId = database.UserId,
                Name = "Blok",
                Goal = Goal.Strength,
                ExperienceLevel = ExperienceLevel.Intermediate,
                PeriodizationModel = PeriodizationModel.LinearRising,
                SetAllocation = SetAllocation.FollowTemplate,
                StartDate = new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc),
                IsActive = true
            };
            mesocycleId = mesocycle.Id;

            for (var weekNumber = 1; weekNumber <= 6; weekNumber++)
            {
                var week = new TrainingWeek { Id = Guid.NewGuid(), WeekNumber = weekNumber, IsDeload = weekNumber == 6 };
                mesocycle.Weeks.Add(week);

                if (weekNumber < 3)
                {
                    continue;
                }

                // Osnovna nedelja (3) nosi staru vežbu sa 6 serija, broj koji je upisalo
                // starije pravilo: iz pete nedelje (5 serija, pomeraj +1) bi se izvelo 4.
                var session = new WorkoutSession
                {
                    Id = Guid.NewGuid(),
                    DayLabel = "Day A",
                    Status = weekNumber == 5 || weekNumber == 6 ? SessionStatus.Planned : SessionStatus.Completed
                };
                var plan = Plan(weekNumber == 3 ? backSquat : frontSquat, weekNumber switch { 3 => 6, 5 => 5, _ => 4 });
                session.ExercisePlans.Add(plan);
                week.Sessions.Add(session);

                if (weekNumber == 4)
                {
                    // Teška nedelja: dve serije uz RIR 0 naspram cilja 2 i dva otkaza - ocena
                    // 0.35 + 0.25 = 0.60, prag srednjeg nivoa.
                    plan.SetLogs.Add(Set(1, 5, 0, false));
                    plan.SetLogs.Add(Set(2, 5, 0, false));
                    plan.SetLogs.Add(Set(3, 3, 0, true));
                    plan.SetLogs.Add(Set(4, 3, 0, true));
                }
            }

            db.Mesocycles.Add(mesocycle);
            await db.SaveChangesAsync();
        }

        var weekFiveSquat = await FindWeekFiveAsync(database);

        await using (var context = database.NewContext())
        {
            var outcome = await new DeloadService(context, new VolumeLandmarkService(context))
                .EvaluatePendingWeeksAsync(database.UserId, mesocycleId, CancellationToken.None);
            await context.SaveChangesAsync();
            Assert.NotNull(outcome);
        }

        await using var read = database.NewContext();
        var deloaded = read.ExercisePlans.Single(plan => plan.Id == weekFiveSquat);

        Assert.True(read.TrainingWeeks.Single(week => week.WeekNumber == 5).IsDeload);
        Assert.Equal(3, deloaded.TargetSets);
    }

    private static async Task<Guid> FindWeekFiveAsync(SqliteDatabase database)
    {
        await using var db = database.NewContext();
        return db.ExercisePlans.Single(plan => plan.WorkoutSession.TrainingWeek.WeekNumber == 5).Id;
    }

    private static Exercise Exercise(Guid id, string name, Guid quads)
    {
        return new Exercise
        {
            Id = id,
            Name = name,
            Type = ExerciseType.Compound,
            Equipment = "Barbell",
            WeightStepKg = 2.5m,
            SuitsLowReps = true,
            Muscles = [new ExerciseMuscle { MuscleGroupId = quads, Contribution = 1.0m }]
        };
    }

    private static ExercisePlan Plan(Guid exerciseId, int prescribedSets)
    {
        return new ExercisePlan
        {
            Id = Guid.NewGuid(),
            ExerciseId = exerciseId,
            Order = 1,
            TargetSets = prescribedSets,
            PrescribedSets = prescribedSets,
            RepRangeMin = 3,
            RepRangeMax = 6,
            BaseRepRangeMin = 3,
            BaseRepRangeMax = 6,
            TargetRir = 2,
            TargetWeightKg = 100m
        };
    }

    private static SetLog Set(int number, int reps, int rir, bool isFailure)
    {
        return new SetLog
        {
            Id = Guid.NewGuid(),
            SetNumber = number,
            WeightKg = 100m,
            Reps = reps,
            Rir = rir,
            IsFailure = isFailure,
            PerformedAt = DateTime.UtcNow
        };
    }
}
