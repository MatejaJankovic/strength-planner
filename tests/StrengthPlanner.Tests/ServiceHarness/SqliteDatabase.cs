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

    /// <summary>
    /// A fresh context, so a test reads what was saved rather than what is tracked. Interceptors let a
    /// test do something in the middle of a call, such as a second writer slipping in between a read
    /// and the save that follows it, which is the only way to make a race happen on purpose.
    /// </summary>
    public AppDbContext NewContext(params Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(interceptors)
            .Options;

        return new AppDbContext(options, new FixedUser(UserId));
    }

    /// <summary>
    /// A second account, for the tests that must show one lifter cannot see or change what another
    /// lifter owns. The harness account stays the one every other test works as.
    /// </summary>
    public Guid AddUser()
    {
        var id = Guid.NewGuid();
        using var db = NewContext();
        db.Users.Add(new ApplicationUser
        {
            Id = id,
            UserName = $"{id}@example.test",
            NormalizedUserName = $"{id}@EXAMPLE.TEST".ToUpperInvariant(),
            Email = $"{id}@example.test",
            NormalizedEmail = $"{id}@EXAMPLE.TEST".ToUpperInvariant()
        });
        db.SaveChanges();
        return id;
    }

    /// <summary>A context that reads and writes as another account (see <see cref="AddUser"/>).</summary>
    public AppDbContext NewContextFor(Guid userId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        return new AppDbContext(options, new FixedUser(userId));
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
