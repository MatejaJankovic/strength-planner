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
    /// <c>null</c> znači „nema korisnika": migracije i seed pri pokretanju, i anonimne
    /// rute. Poređenje sa <c>null</c> u SQL-u nije tačno ni za jedan red, pa u tom stanju
    /// filteri ne propuštaju ništa — što je i namera.
    ///
    /// Namerno <c>null</c>, a ne <see cref="Guid.Empty"/>. Prazan Guid je vrednost kao i
    /// svaka druga: red koji bi ga iz bilo kog razloga poneo u koloni vlasnika postao bi
    /// vidljiv baš u kontekstu bez korisnika. Danas takav red ne može da nastane, ali tu
    /// sigurnost onda nosi podatak, a ne filter.
    /// </summary>
    private Guid? CurrentUserId => _currentUser.UserId;

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
    public DbSet<UserWorkoutTemplate> UserWorkoutTemplates => Set<UserWorkoutTemplate>();
    public DbSet<UserWorkoutTemplateDay> UserWorkoutTemplateDays => Set<UserWorkoutTemplateDay>();

    public DbSet<UserWorkoutTemplateExercise> UserWorkoutTemplateExercises =>
        Set<UserWorkoutTemplateExercise>();

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

        modelBuilder.Entity<UserWorkoutTemplate>()
            .HasQueryFilter(template => template.UserId == CurrentUserId);

        modelBuilder.Entity<UserWorkoutTemplateDay>()
            .HasQueryFilter(day => day.UserWorkoutTemplate.UserId == CurrentUserId);

        modelBuilder.Entity<UserWorkoutTemplateExercise>()
            .HasQueryFilter(exercise =>
                exercise.UserWorkoutTemplateDay.UserWorkoutTemplate.UserId == CurrentUserId);

        // Sistemske vežbe su zajedničke i vidi ih svako; custom vežba pripada onome ko ju
        // je napravio. Isto pravilo koje ExerciseService već piše ručno.
        //
        // Provera da korisnik postoji stoji ispred poređenja zato što su OBE strane
        // nullable: EF za takvo poređenje generiše i granu „oba su NULL", pa bi custom
        // vežba bez upisanog tvorca inače bila vidljiva baš kad korisnika nema.
        modelBuilder.Entity<Exercise>()
            .HasQueryFilter(exercise => !exercise.IsCustom
                                        || (CurrentUserId != null
                                            && exercise.CreatedByUserId == CurrentUserId));

        modelBuilder.Entity<ExerciseMuscle>()
            .HasQueryFilter(link => !link.Exercise.IsCustom
                                    || (CurrentUserId != null
                                        && link.Exercise.CreatedByUserId == CurrentUserId));

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
