using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Infrastructure.Identity;

namespace StrengthPlanner.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<MuscleGroup> MuscleGroups => Set<MuscleGroup>();
    public DbSet<Exercise> Exercises => Set<Exercise>();
    public DbSet<ExerciseMuscle> ExerciseMuscles => Set<ExerciseMuscle>();
    public DbSet<Mesocycle> Mesocycles => Set<Mesocycle>();
    public DbSet<TrainingWeek> TrainingWeeks => Set<TrainingWeek>();
    public DbSet<WorkoutSession> WorkoutSessions => Set<WorkoutSession>();
    public DbSet<ExercisePlan> ExercisePlans => Set<ExercisePlan>();
    public DbSet<SetLog> SetLogs => Set<SetLog>();
    public DbSet<OneRepMaxRecord> OneRepMaxRecords => Set<OneRepMaxRecord>();
    public DbSet<VolumeLandmark> VolumeLandmarks => Set<VolumeLandmark>();
    public DbSet<UserExerciseSetting> UserExerciseSettings => Set<UserExerciseSetting>();
    public DbSet<UserVolumeLandmark> UserVolumeLandmarks => Set<UserVolumeLandmark>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Primeni sve IEntityTypeConfiguration<T> iz ovog assembly-ja (/Persistence/Configurations).
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
