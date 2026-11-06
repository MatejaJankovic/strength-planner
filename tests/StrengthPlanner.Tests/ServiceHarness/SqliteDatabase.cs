using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StrengthPlanner.Application.Interfaces;
using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Domain.Enums;
using StrengthPlanner.Infrastructure.Identity;
using StrengthPlanner.Infrastructure.Persistence;

namespace StrengthPlanner.Tests.ServiceHarness;

/// <summary>
/// A real <see cref="AppDbContext"/> over an in-memory SQLite database, for testing services.
///
/// Rounds 12 and 16 found the same gap three times: a rule that lives in a service query or
/// in a service call site could be reverted with the whole suite green (0 of 613 for the
/// experience level in #74; the stale-maximum flag in #99; the follow-template rule in #101),
/// because no test ever constructed a service. Those rules were checked only live.
///
/// SQLite and not EF's in-memory provider: the services use <c>ExecuteUpdate</c>,
/// <c>ExecuteDelete</c> and transactions, which the in-memory provider does not support. The
/// connection stays open for the life of the object, because an in-memory SQLite database
/// lives exactly as long as its connection.
/// </summary>
internal sealed class SqliteDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        try
        {
            using var db = NewContext();
            db.Database.EnsureCreated();
            db.Users.Add(new ApplicationUser
            {
                Id = UserId,
                UserName = "harness@example.test",
                NormalizedUserName = "HARNESS@EXAMPLE.TEST",
                Email = "harness@example.test",
                NormalizedEmail = "HARNESS@EXAMPLE.TEST"
            });
            db.SaveChanges();
        }
        catch
        {
            _connection.Dispose();
            throw;
        }
    }

    /// <summary>The one account every row belongs to; the ownership filters read it.</summary>
    public Guid UserId { get; } = Guid.NewGuid();

    /// <summary>A fresh context, so a test reads what was saved rather than what is tracked.</summary>
    public AppDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        return new AppDbContext(options, new FixedUser(UserId));
    }

    /// <summary>Adds a system exercise and returns its id.</summary>
    public Guid AddExercise(string name, ExerciseType type = ExerciseType.Compound, string equipment = "Barbell")
    {
        using var db = NewContext();
        var exercise = new Exercise
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = type,
            Equipment = equipment
        };
        db.Exercises.Add(exercise);
        db.SaveChanges();
        return exercise.Id;
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private sealed class FixedUser : ICurrentUser
    {
        public FixedUser(Guid userId)
        {
            UserId = userId;
        }

        public Guid? UserId { get; }
    }
}
