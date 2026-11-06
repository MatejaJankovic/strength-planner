using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Domain.Enums;
using StrengthPlanner.Infrastructure.OneRepMax;

namespace StrengthPlanner.Tests.ServiceHarness;

/// <summary>
/// Ekran treninga i ekran maksimuma kažu koliko je maksimum star samo ako servis postavi
/// oznaku. Pravilo u domenu (<c>OneRepMaxBaseline.IsOutsideLookback</c>) ima svoje testove, ali
/// je red u <c>GetCurrentAsync</c> koji ga poziva mogao da se obriše uz ceo paket zelen (#99).
/// </summary>
public class OneRepMaxServiceTests
{
    [Fact]
    public async Task AMaximumOlderThanTheWindow_IsReportedAsOutsideIt()
    {
        using var database = new SqliteDatabase();
        var row = database.AddExercise("Barbell Row");
        var bench = database.AddExercise("Bench Press");

        await using (var db = database.NewContext())
        {
            db.OneRepMaxRecords.Add(Record(database.UserId, row, 100m, daysAgo: 70));
            db.OneRepMaxRecords.Add(Record(database.UserId, bench, 120m, daysAgo: 10));
            await db.SaveChangesAsync();
        }

        await using var context = database.NewContext();
        var current = await new OneRepMaxService(context).GetCurrentAsync(database.UserId);

        Assert.True(current.Single(dto => dto.ExerciseId == row).IsOutsideLookback);
        Assert.False(current.Single(dto => dto.ExerciseId == bench).IsOutsideLookback);
    }

    [Fact]
    public async Task AFreshTestSet_TakesTheOldMaximumOutOfTheNotice()
    {
        using var database = new SqliteDatabase();
        var row = database.AddExercise("Barbell Row");

        await using (var db = database.NewContext())
        {
            db.OneRepMaxRecords.Add(Record(database.UserId, row, 100m, daysAgo: 70));
            await db.SaveChangesAsync();
        }

        await using (var db = database.NewContext())
        {
            await new OneRepMaxService(db).AddFromSetAsync(
                database.UserId,
                new() { ExerciseId = row, WeightKg = 55m, Reps = 5, Rir = 1 });
        }

        await using var context = database.NewContext();
        var current = await new OneRepMaxService(context).GetCurrentAsync(database.UserId);

        Assert.False(current.Single().IsOutsideLookback);
    }

    private static OneRepMaxRecord Record(Guid userId, Guid exerciseId, decimal valueKg, int daysAgo)
    {
        return new OneRepMaxRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ExerciseId = exerciseId,
            ValueKg = valueKg,
            Source = OneRepMaxSource.Estimated,
            RecordedAt = DateTime.UtcNow.AddDays(-daysAgo)
        };
    }
}
