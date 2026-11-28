using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Infrastructure.Identity;

namespace StrengthPlanner.Tests.ServiceHarness;

/// <summary>
/// <c>deploy/delete-account.sql</c> is how the operator keeps the promise that a deleted account stays
/// deleted after a restore. It is run here against the model's schema, so a table the script forgets,
/// or a foreign key it deletes in the wrong order, fails a test and not an operator.
/// </summary>
public class DeleteAccountScriptTests : IDisposable
{
    private readonly SqliteDatabase _database = new();

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task TheScript_RemovesTheAccountAndWhatItOwns_AndNothingElse()
    {
        var gone = Seed("gone@example.test");
        var stays = Seed("stays@example.test");

        await Run("gone@example.test");

        await using var db = _database.NewContext();
        Assert.False(db.Users.Any(user => user.Id == gone));
        Assert.False(db.Profiles.IgnoreQueryFilters().Any(profile => profile.UserId == gone));
        Assert.False(db.UserWorkoutTemplates.IgnoreQueryFilters().Any(template => template.UserId == gone));
        Assert.False(db.Exercises.IgnoreQueryFilters().Any(exercise => exercise.CreatedByUserId == gone));

        Assert.True(db.Users.Any(user => user.Id == stays));
        Assert.True(db.Profiles.IgnoreQueryFilters().Any(profile => profile.UserId == stays));
        Assert.True(db.UserWorkoutTemplates.IgnoreQueryFilters().Any(template => template.UserId == stays));
        Assert.True(db.Exercises.IgnoreQueryFilters().Any(exercise => exercise.CreatedByUserId == stays));
    }

    private Guid Seed(string email)
    {
        var id = Guid.NewGuid();
        using var db = _database.NewContext();
        db.Users.Add(new ApplicationUser
        {
            Id = id,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            Profile = new Profile { Age = 30, BodyweightKg = 80 }
        });
        var exercise = new Exercise
        {
            Id = Guid.NewGuid(),
            Name = email,
            Equipment = "Barbell",
            IsCustom = true,
            CreatedByUserId = id
        };
        db.Exercises.Add(exercise);
        // The template holds the account's own exercise, so it has to go before that exercise does.
        db.UserWorkoutTemplates.Add(new UserWorkoutTemplate
        {
            Id = Guid.NewGuid(),
            UserId = id,
            Name = email,
            Days =
            {
                new UserWorkoutTemplateDay
                {
                    Id = Guid.NewGuid(),
                    Name = "A",
                    Exercises = { new UserWorkoutTemplateExercise { Id = Guid.NewGuid(), ExerciseId = exercise.Id, Sets = 3 } }
                }
            }
        });
        db.SaveChanges();
        return id;
    }

    private async Task Run(string email)
    {
        var script = File.ReadAllText(FindScript());

        // psql puts the value in as a quoted literal where `:'email'` stands; here it is a parameter.
        Assert.Contains(":'email'", script);
        await using var db = _database.NewContext();
        await db.Database.ExecuteSqlRawAsync(
            script.Replace(":'email'", "@email"), new SqliteParameter("@email", email));
    }

    private static string FindScript()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "deploy", "delete-account.sql");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("deploy/delete-account.sql was not found.");
    }
}
