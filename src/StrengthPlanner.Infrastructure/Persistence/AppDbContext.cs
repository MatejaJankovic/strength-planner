using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using StrengthPlanner.Application.Interfaces;
using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Infrastructure.Identity;

namespace StrengthPlanner.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    private readonly ICurrentUser _currentUser;

    /// <summary>
    /// Vlasnik redova koje ovaj kontekst sme da vidi. EF Core ovo čita kao parametar
    /// upita, pa isti prevedeni upit radi za sve korisnike, sa vrednošću koja se menja.
    ///
    /// Čita se pri svakom upitu, a NE jednom u konstruktoru. Razlika je bitna: kontekst
    /// nastane već pri proveri tokena — tada JwtBearer događaj traži
    /// <c>UserManager</c> da uporedi security stamp — a u tom trenutku
    /// <c>HttpContext.User</c> još nije postavljen. Vrednost zapamćena tada ostala bi
    /// prazna do kraja zahteva, pa bi korisnik svoje podatke video kao nepostojeće.
    ///
    /// <see cref="Guid.Empty"/> znači „nema korisnika": migracije i seed pri pokretanju,
    /// i anonimne rute. Nijedan korisnički red nema takvog vlasnika, pa u tom stanju
    /// filteri ne propuštaju ništa — što je i namera.
    /// </summary>
    private Guid CurrentUserId => _currentUser.UserId ?? Guid.Empty;

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUser currentUser)
        : base(options)
    {
        _currentUser = currentUser;
    }

    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<MuscleGroup> MuscleGroups => Set<MuscleGroup>();
    public DbSet<Exercise> Exercises => Set<Exercise>();
    public DbSet<ExerciseMuscle> ExerciseMuscles => Set<ExerciseMuscle>();
    public DbSet<Macrocycle> Macrocycles => Set<Macrocycle>();
    public DbSet<MacrocycleBlock> MacrocycleBlocks => Set<MacrocycleBlock>();
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

        ApplyOwnershipFilters(modelBuilder);
    }

    /// <summary>
    /// Ograničava svaki upit nad korisničkim podacima na vlasnika, na nivou modela.
    ///
    /// Servisi i dalje pišu svoj uslov po <c>userId</c> i to ostaje glavna provera; ovo je
    /// sloj ispod nje, za slučaj da se negde izostavi. Takav propust nije hipotetičan:
    /// <c>MacrocycleService.RegenerateBlockAsync</c> dohvata blok samo po njegovom
    /// identifikatoru. Tamo je bezbedno jer je blok neposredno pre toga preuzet upitom
    /// koji jeste ograničen na korisnika — ali ta bezbednost živi u redosledu poziva, a
    /// ne u samom upitu, i prva izmena reda poteza je gubi.
    ///
    /// Filteri se pišu preko navigacija do vlasnika, pa tabele bez svoje <c>UserId</c>
    /// kolone (nedelje, treninzi, planovi vežbi, serije) dobijaju isto pravilo kao i
    /// mezociklus kome pripadaju.
    /// </summary>
    private void ApplyOwnershipFilters(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Profile>()
            .HasQueryFilter(profile => profile.UserId == CurrentUserId);

        modelBuilder.Entity<Mesocycle>()
            .HasQueryFilter(mesocycle => mesocycle.UserId == CurrentUserId);

        modelBuilder.Entity<Macrocycle>()
            .HasQueryFilter(macrocycle => macrocycle.UserId == CurrentUserId);

        modelBuilder.Entity<OneRepMaxRecord>()
            .HasQueryFilter(record => record.UserId == CurrentUserId);

        modelBuilder.Entity<UserExerciseSetting>()
            .HasQueryFilter(setting => setting.UserId == CurrentUserId);

        modelBuilder.Entity<UserVolumeLandmark>()
            .HasQueryFilter(landmark => landmark.UserId == CurrentUserId);

        // Sistemske vežbe su zajedničke i vidi ih svako; custom vežba pripada onome ko ju
        // je napravio. Isto pravilo koje ExerciseService već piše ručno.
        modelBuilder.Entity<Exercise>()
            .HasQueryFilter(exercise => !exercise.IsCustom || exercise.CreatedByUserId == CurrentUserId);

        modelBuilder.Entity<ExerciseMuscle>()
            .HasQueryFilter(link => !link.Exercise.IsCustom || link.Exercise.CreatedByUserId == CurrentUserId);

        modelBuilder.Entity<MacrocycleBlock>()
            .HasQueryFilter(block => block.Macrocycle.UserId == CurrentUserId);

        modelBuilder.Entity<TrainingWeek>()
            .HasQueryFilter(week => week.Mesocycle.UserId == CurrentUserId);

        modelBuilder.Entity<WorkoutSession>()
            .HasQueryFilter(session => session.TrainingWeek.Mesocycle.UserId == CurrentUserId);

        modelBuilder.Entity<ExercisePlan>()
            .HasQueryFilter(plan => plan.WorkoutSession.TrainingWeek.Mesocycle.UserId == CurrentUserId);

        modelBuilder.Entity<SetLog>()
            .HasQueryFilter(log => log.ExercisePlan.WorkoutSession.TrainingWeek.Mesocycle.UserId == CurrentUserId);
    }
}
