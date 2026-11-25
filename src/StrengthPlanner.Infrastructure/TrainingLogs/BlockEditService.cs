using Microsoft.EntityFrameworkCore;
using StrengthPlanner.Application.DTOs.Sessions;
using StrengthPlanner.Application.Exceptions;
using StrengthPlanner.Application.Interfaces;
using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Domain.Enums;
using StrengthPlanner.Infrastructure.Mesocycles;
using StrengthPlanner.Infrastructure.Persistence;

namespace StrengthPlanner.Infrastructure.TrainingLogs;

/// <summary>
/// Dodavanje i uklanjanje vežbe u bloku koji već teče: promena važi za isti dan u svakom treningu
/// koji još čeka, od izabranog do kraja bloka, kao i zamena vežbe (<see cref="ExerciseSubstitutionService"/>).
///
/// Prošlost se ne dira. Trening koji je završen, preskočen ili ima upisanu seriju ostaje kakav je bio,
/// jer je to istorija iz koje se uče procena maksimuma, progresija i granice volumena.
///
/// Dodata vežba dobija propis koji bi blok imao za nju da je od početka bila u šablonu
/// (<see cref="ExercisePrescriptions"/>): isti opseg, RIR i broj serija kao svaka druga vežba tog
/// tipa u toj nedelji. Opterećenje se ne upisuje, nego se računa pri čitanju iz maksimuma, kao za
/// svaku vežbu koja još nema odrađen trening. Posle svake izmene nedelje se ponovo balansiraju, osim
/// kada serije prate lični šablon, jer se volumen mišića pomera čim vežba dođe ili ode.
/// </summary>
public sealed class BlockEditService : IBlockEditService
{
    private readonly AppDbContext _db;
    private readonly WeeklySetPlanner _setPlanner;

    public BlockEditService(AppDbContext db, WeeklySetPlanner setPlanner)
    {
        _db = db;
        _setPlanner = setPlanner;
    }

    public async Task<AddableExercisesDto> GetAddableAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var context = await LoadEditableSessionAsync(userId, sessionId, cancellationToken);

        if (await HadAutoDeloadAsync(userId, context, cancellationToken))
        {
            return new AddableExercisesDto { Unavailable = AutoDeloadRefusal };
        }

        var inSession = await _db.ExercisePlans
            .AsNoTracking()
            .Where(plan => plan.WorkoutSessionId == sessionId)
            .Select(plan => plan.ExerciseId)
            .ToListAsync(cancellationToken);

        var exercises = await _db.Exercises
            .AsNoTracking()
            .Where(exercise => !exercise.IsCustom || exercise.CreatedByUserId == userId)
            .Select(exercise => new
            {
                exercise.Id,
                exercise.Name,
                exercise.Equipment,
                exercise.IsCustom,
                exercise.BodyweightShare
            })
            .ToListAsync(cancellationToken);

        // Udeli se čitaju u memoriji: poređenje decimalnog udela u upitu SQLite poredi kao tekst.
        var links = await _db.ExerciseMuscles
            .AsNoTracking()
            .Select(link => new { link.ExerciseId, link.MuscleGroupId, link.Contribution })
            .ToListAsync(cancellationToken);
        var muscleNames = await _db.MuscleGroups
            .AsNoTracking()
            .ToDictionaryAsync(group => group.Id, group => group.Name, cancellationToken);
        var primaryByExerciseId = links
            .Where(link => link.Contribution == 1.0m && muscleNames.ContainsKey(link.MuscleGroupId))
            .GroupBy(link => link.ExerciseId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group
                    .Select(link => muscleNames[link.MuscleGroupId])
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToList());

        return new AddableExercisesDto
        {
            Exercises = exercises
                .Where(exercise => !inSession.Contains(exercise.Id) && IsLoadable(exercise.Equipment, exercise.BodyweightShare))
                .OrderBy(exercise => exercise.Name, StringComparer.Ordinal)
                .Select(exercise => new AddableExerciseDto
                {
                    ExerciseId = exercise.Id,
                    Name = exercise.Name,
                    Equipment = exercise.Equipment,
                    IsCustom = exercise.IsCustom,
                    PrimaryMuscles = primaryByExerciseId.TryGetValue(exercise.Id, out var primaries) ? primaries : []
                })
                .ToList()
        };
    }

    public async Task<Guid> AddExerciseAsync(
        Guid userId,
        Guid sessionId,
        Guid exerciseId,
        CancellationToken cancellationToken = default)
    {
        // Provera i upis idu zajedno: serija upisana u međuvremenu ne sme da završi pod dodatom vežbom
        // a nedelje se balansiraju samo ako su sve izmene ušle.
        await using var transaction = _db.Database.CurrentTransaction is null
            ? await _db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var context = await LoadEditableSessionAsync(userId, sessionId, cancellationToken);

        if (await HadAutoDeloadAsync(userId, context, cancellationToken))
        {
            throw new TrainingLogException(TrainingLogErrorType.Conflict, AutoDeloadRefusal);
        }

        var exercise = await _db.Exercises
            .AsNoTracking()
            .Where(candidate => candidate.Id == exerciseId
                                && (!candidate.IsCustom || candidate.CreatedByUserId == userId))
            .FirstOrDefaultAsync(cancellationToken);

        if (exercise is null)
        {
            throw new TrainingLogException(TrainingLogErrorType.Validation, "Vežba ne postoji.");
        }

        if (!IsLoadable(exercise.Equipment, exercise.BodyweightShare))
        {
            throw new TrainingLogException(
                TrainingLogErrorType.Validation,
                "Ova vežba se ne radi sa opterećenjem, pa je plan ne može propisati.");
        }

        // Svaki trening istog dana koji još čeka, od ovog do kraja bloka. Ovaj trening je uključen i
        // kada je u toku, jer se u njemu još ništa ne računa dok serije nisu upisane na dodatu vežbu.
        var targets = await LoadTargetSessionsAsync(userId, context, cancellationToken);

        if (context.ExerciseIds.Contains(exerciseId))
        {
            throw new TrainingLogException(TrainingLogErrorType.Validation, "Ova vežba je već u treningu.");
        }

        if (context.ExerciseIds.Count >= TrainingConstants.MaxTemplateExercisesPerDay)
        {
            throw new TrainingLogException(
                TrainingLogErrorType.Conflict,
                $"Dan može da ima najviše {TrainingConstants.MaxTemplateExercisesPerDay} vežbi.");
        }

        foreach (var target in targets)
        {
            // Trening koji novu vežbu već ima ili je pun ne dobija je: ostali ipak dobijaju.
            if (target.ExercisePlans.Any(plan => plan.ExerciseId == exerciseId)
                || target.ExercisePlans.Count >= TrainingConstants.MaxTemplateExercisesPerDay)
            {
                continue;
            }

            var prescribed = ExercisePrescriptions.For(
                context.Goal,
                exercise.Type,
                exercise.SuitsLowReps,
                context.PeriodizationModel,
                context.ExperienceLevel,
                target.TrainingWeek.WeekNumber);

            _db.ExercisePlans.Add(new ExercisePlan
            {
                Id = Guid.NewGuid(),
                WorkoutSessionId = target.Id,
                ExerciseId = exerciseId,
                // Na kraj dana: vežbe koje su već u treningu zadržavaju mesto (a prva je i kalibraciona).
                Order = target.ExercisePlans.Count == 0 ? 1 : target.ExercisePlans.Max(plan => plan.Order) + 1,
                TargetSets = prescribed.Week.Sets,
                PrescribedSets = prescribed.Week.Sets,
                RepRangeMin = prescribed.Week.RepRangeMin,
                RepRangeMax = prescribed.Week.RepRangeMax,
                BaseRepRangeMin = prescribed.BaseRepRangeMin,
                BaseRepRangeMax = prescribed.BaseRepRangeMax,
                TargetRir = prescribed.Week.TargetRir,
                TargetWeightKg = null
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        await RebalanceAsync(userId, context, cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return sessionId;
    }

    public async Task<Guid> RemoveExerciseAsync(
        Guid userId,
        Guid exercisePlanId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = _db.Database.CurrentTransaction is null
            ? await _db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var plan = await _db.ExercisePlans
            .AsNoTracking()
            .Where(candidate => candidate.Id == exercisePlanId
                                && candidate.WorkoutSession.TrainingWeek.Mesocycle.UserId == userId)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.ExerciseId,
                candidate.WorkoutSessionId,
                HasSets = candidate.SetLogs.Any()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (plan is null)
        {
            throw new TrainingLogException(TrainingLogErrorType.NotFound, "Exercise plan was not found.");
        }

        var context = await LoadEditableSessionAsync(userId, plan.WorkoutSessionId, cancellationToken);

        if (plan.HasSets)
        {
            throw new TrainingLogException(
                TrainingLogErrorType.Conflict,
                "Vežba se uklanja samo dok na njoj nema upisane serije.");
        }

        var targets = await LoadTargetSessionsAsync(userId, context, cancellationToken);
        var affected = targets
            .Where(target => target.ExercisePlans.Any(other => other.ExerciseId == plan.ExerciseId))
            .ToList();

        // Trening bez vežbe nije trening: ni jedan od onih koji bi ostao prazan ne sme da se izmeni,
        // jer bi dan u nekim nedeljama ostao bez sadržaja a u drugim ne.
        if (affected.Any(target => target.ExercisePlans.Count <= 1))
        {
            throw new TrainingLogException(
                TrainingLogErrorType.Conflict,
                "Trening mora da ima bar jednu vežbu. Dodaj drugu vežbu pre nego što ukloniš ovu.");
        }

        foreach (var target in affected)
        {
            // Na planu nema serije: trening sa upisanom serijom nije meta (LoadTargetSessionsAsync), a
            // izabrani je proveren iznad. Zato se ovde ništa ne preskače.
            var removed = target.ExercisePlans.Where(other => other.ExerciseId == plan.ExerciseId).ToList();

            foreach (var item in removed)
            {
                _db.ExercisePlans.Remove(item);
                target.ExercisePlans.Remove(item);
            }

            // Redosled se zbija: prvi u danu je ujedno i kalibraciona vežba (CalibrationSet), pa
            // rupa na početku ne sme da ostane.
            var order = 1;

            foreach (var remaining in target.ExercisePlans.OrderBy(other => other.Order))
            {
                remaining.Order = order++;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        await RebalanceAsync(userId, context, cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return plan.WorkoutSessionId;
    }

    /// <summary>
    /// Trening koji se menja: mora biti vlasnikov i da još čeka ili je u toku. Završen ili preskočen
    /// trening je istorija i ne menja se.
    /// </summary>
    private async Task<EditContext> LoadEditableSessionAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var session = await _db.WorkoutSessions
            .AsNoTracking()
            .Where(candidate => candidate.Id == sessionId
                                && candidate.TrainingWeek.Mesocycle.UserId == userId)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.DayLabel,
                candidate.Status,
                candidate.TrainingWeek.WeekNumber,
                candidate.TrainingWeek.MesocycleId,
                candidate.TrainingWeek.Mesocycle.Goal,
                candidate.TrainingWeek.Mesocycle.PeriodizationModel,
                candidate.TrainingWeek.Mesocycle.ExperienceLevel,
                candidate.TrainingWeek.Mesocycle.SetAllocation
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (session is null)
        {
            throw new TrainingLogException(TrainingLogErrorType.NotFound, "Workout session was not found.");
        }

        if (session.Status is not (SessionStatus.Planned or SessionStatus.InProgress))
        {
            throw new TrainingLogException(
                TrainingLogErrorType.Conflict,
                "Vežbe se menjaju samo u treningu koji još čeka ili je u toku.");
        }

        var exerciseIds = await _db.ExercisePlans
            .AsNoTracking()
            .Where(plan => plan.WorkoutSessionId == sessionId)
            .Select(plan => plan.ExerciseId)
            .ToListAsync(cancellationToken);

        return new EditContext(
            session.Id,
            session.DayLabel,
            session.WeekNumber,
            session.MesocycleId,
            session.Goal,
            session.PeriodizationModel,
            session.ExperienceLevel,
            session.SetAllocation,
            exerciseIds.ToHashSet());
    }

    /// <summary>
    /// Blok koji je već imao deload zbog umora ne prima nove vežbe. Posle njega nedelje ne prate
    /// raspored bloka: oslobođena planirana nedelja nosi fazu one koja je postala deload, pa propis
    /// po broju nedelje ne bi bio onaj koji nedelja zaista nosi.
    /// </summary>
    private async Task<bool> HadAutoDeloadAsync(
        Guid userId,
        EditContext context,
        CancellationToken cancellationToken)
    {
        return await _db.TrainingWeeks
            .AsNoTracking()
            .AnyAsync(
                week => week.MesocycleId == context.MesocycleId
                        && week.Mesocycle.UserId == userId
                        && week.IsAutoDeload,
                cancellationToken);
    }

    private const string AutoDeloadRefusal =
        "Ovaj blok je već imao rasterećenje zbog umora, pa se u njemu više ne dodaju vežbe. Dodaj vežbu u sledećem bloku.";

    /// <summary>
    /// Isti dan bloka od izabranog treninga do kraja, u treninzima koji još čekaju. Izabrani je
    /// uključen i kada je u toku, a trening sa upisanom serijom nikada nije meta izmene.
    /// </summary>
    private async Task<List<WorkoutSession>> LoadTargetSessionsAsync(
        Guid userId,
        EditContext context,
        CancellationToken cancellationToken)
    {
        return await _db.WorkoutSessions
            .Include(session => session.TrainingWeek)
            .Include(session => session.ExercisePlans)
            .Where(session => session.TrainingWeek.MesocycleId == context.MesocycleId
                              && session.TrainingWeek.Mesocycle.UserId == userId
                              && session.DayLabel == context.DayLabel
                              && session.TrainingWeek.WeekNumber >= context.WeekNumber
                              && (session.Status == SessionStatus.Planned || session.Id == context.SessionId)
                              && (session.Id == context.SessionId || !session.ExercisePlans.Any(plan => plan.SetLogs.Any())))
            .OrderBy(session => session.TrainingWeek.WeekNumber)
            .ToListAsync(cancellationToken);
    }

    private async Task RebalanceAsync(Guid userId, EditContext context, CancellationToken cancellationToken)
    {
        // Volumen mišića se pomerio, pa se nedelje koje još čekaju ponovo balansiraju - osim kada je
        // vežbač izabrao da serije prate njegov šablon.
        if (context.SetAllocation != SetAllocation.TargetVolume)
        {
            return;
        }

        await _setPlanner.RebalanceAsync(userId, context.MesocycleId, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Isto pravilo kao kod zamene: vežba bez opterećenja (na primer plank) nema šta da se propiše.
    /// </summary>
    private static bool IsLoadable(string equipment, decimal bodyweightShare)
    {
        return !(string.Equals(equipment, "Bodyweight", StringComparison.OrdinalIgnoreCase) && bodyweightShare == 0m);
    }

    private sealed record EditContext(
        Guid SessionId,
        string DayLabel,
        int WeekNumber,
        Guid MesocycleId,
        Goal Goal,
        PeriodizationModel PeriodizationModel,
        ExperienceLevel ExperienceLevel,
        SetAllocation SetAllocation,
        IReadOnlySet<Guid> ExerciseIds);
}
