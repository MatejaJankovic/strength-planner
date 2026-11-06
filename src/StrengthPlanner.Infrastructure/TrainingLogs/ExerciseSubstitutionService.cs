using Microsoft.EntityFrameworkCore;
using StrengthPlanner.Application.DTOs.Sessions;
using StrengthPlanner.Application.Exceptions;
using StrengthPlanner.Application.Interfaces;
using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Enums;
using StrengthPlanner.Infrastructure.Mesocycles;
using StrengthPlanner.Infrastructure.Persistence;

namespace StrengthPlanner.Infrastructure.TrainingLogs;

/// <summary>
/// Zamena vežbe za ostatak bloka (A2): vežba se menja u svakom treningu istog dana koji još
/// čeka, od izabranog treninga do kraja bloka.
///
/// Menja se samo vežba. Opseg, RIR i serije ostaju propis bloka za to mesto, pa je zamena
/// dozvoljena samo vežbi kojoj bi blok propisao isto (<see cref="ExerciseSubstitution"/>).
/// Opterećenje se briše: računa se pri čitanju iz maksimuma nove vežbe, kao za svaku vežbu
/// koja još nema odrađen trening, a posle prvog odrađenog dana ga vodi progresija. Prošlost se
/// ne dira: trening koji je počeo ili ima upisanu seriju ostaje sa vežbom koju je radio.
/// </summary>
public sealed class ExerciseSubstitutionService : IExerciseSubstitutionService
{
    private readonly AppDbContext _db;
    private readonly WeeklySetPlanner _setPlanner;

    public ExerciseSubstitutionService(AppDbContext db, WeeklySetPlanner setPlanner)
    {
        _db = db;
        _setPlanner = setPlanner;
    }

    public async Task<IReadOnlyList<ExerciseAlternativeDto>> GetAlternativesAsync(
        Guid userId,
        Guid exercisePlanId,
        CancellationToken cancellationToken = default)
    {
        var plan = await LoadReplaceablePlanAsync(userId, exercisePlanId, cancellationToken);
        var exercises = await LoadExercisesAsync(userId, cancellationToken);

        return Alternatives(plan, exercises)
            .OrderBy(exercise => exercise.Name)
            .Select(exercise => new ExerciseAlternativeDto
            {
                ExerciseId = exercise.Profile.ExerciseId,
                Name = exercise.Name,
                Equipment = exercise.Equipment,
                IsCustom = exercise.IsCustom
            })
            .ToList();
    }

    public async Task<Guid> SubstituteAsync(
        Guid userId,
        Guid exercisePlanId,
        Guid exerciseId,
        CancellationToken cancellationToken = default)
    {
        var plan = await LoadReplaceablePlanAsync(userId, exercisePlanId, cancellationToken);
        var exercises = await LoadExercisesAsync(userId, cancellationToken);

        if (!Alternatives(plan, exercises).Any(exercise => exercise.Profile.ExerciseId == exerciseId))
        {
            throw new TrainingLogException(
                TrainingLogErrorType.Validation,
                "Ova vežba ne može da zameni planiranu: mora da radi isti mišić, da bude istog tipa i da je blok propisuje isto.");
        }

        // Isti dan bloka od ovog treninga do kraja, u treninzima koji još čekaju. Ovaj plan je
        // uključen i kad je njegov trening započet, ako na njemu još nema serije.
        var targets = await _db.ExercisePlans
            .Include(other => other.WorkoutSession)
                .ThenInclude(session => session.ExercisePlans)
            .Where(other => other.WorkoutSession.TrainingWeek.MesocycleId == plan.MesocycleId
                            && other.WorkoutSession.TrainingWeek.Mesocycle.UserId == userId
                            && other.WorkoutSession.DayLabel == plan.DayLabel
                            && other.WorkoutSession.TrainingWeek.WeekNumber >= plan.WeekNumber
                            && other.ExerciseId == plan.ExerciseId
                            && !other.SetLogs.Any()
                            && (other.WorkoutSession.Status == SessionStatus.Planned || other.Id == plan.PlanId))
            .ToListAsync(cancellationToken);

        foreach (var target in targets)
        {
            // Trening koji novu vežbu već ima ne dobija je dvaput.
            if (target.WorkoutSession.ExercisePlans.Any(other => other.ExerciseId == exerciseId))
            {
                continue;
            }

            target.ExerciseId = exerciseId;
            target.TargetWeightKg = null;
        }

        await _db.SaveChangesAsync(cancellationToken);

        // Drugi mišić sekundarno (na primer veslanje sa šipkom i na sajli rade biceps isto, ali
        // ne svaka zamena) menja nedelju, pa se ona ponovo balansira - osim kad je vežbač
        // izabrao da serije prate njegov šablon.
        if (plan.SetAllocation == SetAllocation.TargetVolume)
        {
            await _setPlanner.RebalanceAsync(userId, plan.MesocycleId, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return plan.SessionId;
    }

    private static IEnumerable<ExerciseOption> Alternatives(
        ReplaceablePlan plan,
        IReadOnlyDictionary<Guid, ExerciseOption> exercises)
    {
        if (!exercises.TryGetValue(plan.ExerciseId, out var current))
        {
            return [];
        }

        return exercises.Values.Where(candidate =>
            ExerciseSubstitution.CanReplace(current.Profile, candidate.Profile)
            && !plan.SessionExerciseIds.Contains(candidate.Profile.ExerciseId));
    }

    private async Task<ReplaceablePlan> LoadReplaceablePlanAsync(
        Guid userId,
        Guid exercisePlanId,
        CancellationToken cancellationToken)
    {
        var plan = await _db.ExercisePlans
            .AsNoTracking()
            .Where(candidate => candidate.Id == exercisePlanId
                                && candidate.WorkoutSession.TrainingWeek.Mesocycle.UserId == userId)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.ExerciseId,
                candidate.WorkoutSessionId,
                candidate.WorkoutSession.DayLabel,
                candidate.WorkoutSession.Status,
                candidate.WorkoutSession.TrainingWeek.WeekNumber,
                candidate.WorkoutSession.TrainingWeek.MesocycleId,
                candidate.WorkoutSession.TrainingWeek.Mesocycle.SetAllocation,
                HasSets = candidate.SetLogs.Any()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (plan is null)
        {
            throw new TrainingLogException(TrainingLogErrorType.NotFound, "Exercise plan was not found.");
        }

        // Zamena gleda unapred. Završen ili preskočen trening je istorija, a vežba na kojoj je
        // serija već upisana se radi.
        if (plan.HasSets || plan.Status is not (SessionStatus.Planned or SessionStatus.InProgress))
        {
            throw new TrainingLogException(
                TrainingLogErrorType.Conflict,
                "Vežba se menja samo u treningu koji još čeka i dok na njoj nema upisane serije.");
        }

        var sessionExerciseIds = await _db.ExercisePlans
            .AsNoTracking()
            .Where(other => other.WorkoutSessionId == plan.WorkoutSessionId)
            .Select(other => other.ExerciseId)
            .ToListAsync(cancellationToken);

        return new ReplaceablePlan(
            plan.Id,
            plan.ExerciseId,
            plan.WorkoutSessionId,
            plan.DayLabel,
            plan.WeekNumber,
            plan.MesocycleId,
            plan.SetAllocation,
            sessionExerciseIds.ToHashSet());
    }

    /// <summary>Sistemske vežbe i vežbe ovog korisnika, sa onim što pravilo zamene čita.</summary>
    private async Task<IReadOnlyDictionary<Guid, ExerciseOption>> LoadExercisesAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var exercises = await _db.Exercises
            .AsNoTracking()
            .Where(exercise => !exercise.IsCustom || exercise.CreatedByUserId == userId)
            .Select(exercise => new
            {
                exercise.Id,
                exercise.Name,
                exercise.Equipment,
                exercise.IsCustom,
                exercise.Type,
                exercise.SuitsLowReps,
                exercise.BodyweightShare
            })
            .ToListAsync(cancellationToken);

        // Udeli se čitaju u memoriji: poređenje decimalnog udela u upitu SQLite poredi kao tekst.
        var muscles = await _db.ExerciseMuscles
            .AsNoTracking()
            .Select(link => new { link.ExerciseId, link.MuscleGroupId, link.Contribution })
            .ToListAsync(cancellationToken);
        var primaryByExerciseId = muscles
            .Where(link => link.Contribution == 1.0m)
            .GroupBy(link => link.ExerciseId)
            .ToDictionary(group => group.Key, group => group.First().MuscleGroupId);

        return exercises.ToDictionary(
            exercise => exercise.Id,
            exercise => new ExerciseOption(
                new SubstitutionProfile(
                    exercise.Id,
                    exercise.Type,
                    exercise.SuitsLowReps,
                    primaryByExerciseId.TryGetValue(exercise.Id, out var primary) ? primary : null,
                    IsLoadable: !(string.Equals(exercise.Equipment, "Bodyweight", StringComparison.OrdinalIgnoreCase)
                                  && exercise.BodyweightShare == 0m)),
                exercise.Name,
                exercise.Equipment,
                exercise.IsCustom));
    }

    private sealed record ReplaceablePlan(
        Guid PlanId,
        Guid ExerciseId,
        Guid SessionId,
        string DayLabel,
        int WeekNumber,
        Guid MesocycleId,
        SetAllocation SetAllocation,
        IReadOnlySet<Guid> SessionExerciseIds);

    private sealed record ExerciseOption(SubstitutionProfile Profile, string Name, string Equipment, bool IsCustom);
}
