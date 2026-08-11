using Microsoft.EntityFrameworkCore;
using StrengthPlanner.Application.DTOs.Exercises;
using StrengthPlanner.Application.Exceptions;
using StrengthPlanner.Application.Interfaces;
using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Domain.Enums;
using StrengthPlanner.Infrastructure.Persistence;

namespace StrengthPlanner.Infrastructure.Exercises;

public class ExerciseService : IExerciseService
{
    private const decimal PrimaryContribution = 1.0m;
    private const decimal SecondaryContribution = 0.5m;

    private readonly AppDbContext _db;

    public ExerciseService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ExerciseDto>> GetAllAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var exercises = await _db.Exercises
            .AsNoTracking()
            .Where(e => !e.IsCustom || e.CreatedByUserId == userId)
            .OrderBy(e => e.Name)
            .Select(e => new
            {
                e.Id,
                e.Name,
                e.Type,
                e.Equipment,
                e.IsCustom,
                e.WeightStepKg,
                Muscles = e.Muscles
                    .OrderByDescending(m => m.Contribution)
                    .Select(m => new MuscleContributionDto
                    {
                        MuscleGroup = m.MuscleGroup.Name,
                        Contribution = m.Contribution
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        var overrideByExerciseId = await GetWeightStepOverridesAsync(userId, cancellationToken);

        return exercises
            .Select(exercise => new ExerciseDto
            {
                Id = exercise.Id,
                Name = exercise.Name,
                Type = exercise.Type.ToString(),
                Equipment = exercise.Equipment,
                IsCustom = exercise.IsCustom,
                WeightStepKg = overrideByExerciseId.TryGetValue(exercise.Id, out var stepOverride)
                    ? stepOverride
                    : exercise.WeightStepKg,
                DefaultWeightStepKg = exercise.WeightStepKg,
                IsWeightStepOverridden = overrideByExerciseId.ContainsKey(exercise.Id),
                Muscles = exercise.Muscles
            })
            .ToList();
    }

    /// <summary>
    /// Efektivni korak opterećenja po vežbi: korisnički override ako postoji,
    /// inače podrazumevani korak same vežbe.
    /// </summary>
    private async Task<Dictionary<Guid, decimal>> GetWeightStepOverridesAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await _db.UserExerciseSettings
            .AsNoTracking()
            .Where(setting => setting.UserId == userId)
            .ToDictionaryAsync(
                setting => setting.ExerciseId,
                setting => setting.WeightStepKg,
                cancellationToken);
    }

    public async Task<ExerciseDto> SetWeightStepAsync(
        Guid userId,
        Guid exerciseId,
        decimal? weightStepKg,
        CancellationToken cancellationToken = default)
    {
        var exercise = await _db.Exercises
            .Include(e => e.Muscles)
                .ThenInclude(m => m.MuscleGroup)
            .FirstOrDefaultAsync(
                e => e.Id == exerciseId && (!e.IsCustom || e.CreatedByUserId == userId),
                cancellationToken);

        if (exercise is null)
        {
            throw new TrainingLogException(TrainingLogErrorType.NotFound, "Exercise was not found.");
        }

        if (weightStepKg.HasValue && !EquipmentWeightStep.IsValidStep(weightStepKg.Value))
        {
            throw new TrainingLogException(
                TrainingLogErrorType.Validation,
                $"Weight step must be between {EquipmentWeightStep.MinStepKg} and {EquipmentWeightStep.MaxStepKg} kg.");
        }

        var setting = await _db.UserExerciseSettings.FirstOrDefaultAsync(
            s => s.UserId == userId && s.ExerciseId == exerciseId,
            cancellationToken);

        // Null ili vrednost jednaka podrazumevanoj: override nema smisla da postoji.
        var isReset = !weightStepKg.HasValue || weightStepKg.Value == exercise.WeightStepKg;

        if (isReset)
        {
            if (setting is not null)
            {
                _db.UserExerciseSettings.Remove(setting);
            }
        }
        else if (setting is null)
        {
            _db.UserExerciseSettings.Add(new UserExerciseSetting
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ExerciseId = exerciseId,
                WeightStepKg = weightStepKg!.Value
            });
        }
        else
        {
            setting.WeightStepKg = weightStepKg!.Value;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new ExerciseDto
        {
            Id = exercise.Id,
            Name = exercise.Name,
            Type = exercise.Type.ToString(),
            Equipment = exercise.Equipment,
            IsCustom = exercise.IsCustom,
            WeightStepKg = isReset ? exercise.WeightStepKg : weightStepKg!.Value,
            DefaultWeightStepKg = exercise.WeightStepKg,
            IsWeightStepOverridden = !isReset,
            Muscles = exercise.Muscles
                .OrderByDescending(m => m.Contribution)
                .Select(m => new MuscleContributionDto
                {
                    MuscleGroup = m.MuscleGroup.Name,
                    Contribution = m.Contribution
                })
                .ToList()
        };
    }

    public async Task<ExerciseDto> CreateCustomAsync(
        Guid userId,
        CreateExerciseRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var name = request.Name.Trim();
        if (name.Length == 0)
        {
            throw new TrainingLogException(TrainingLogErrorType.Validation, "Exercise name is required.");
        }

        if (!Enum.TryParse<ExerciseType>(request.Type, ignoreCase: true, out var exerciseType)
            || !Enum.IsDefined(exerciseType))
        {
            throw new TrainingLogException(
                TrainingLogErrorType.Validation,
                "Exercise type must be 'Compound' or 'Isolation'.");
        }

        if (request.Muscles.Count == 0)
        {
            throw new TrainingLogException(TrainingLogErrorType.Validation, "At least one muscle group is required.");
        }

        var muscleNames = request.Muscles.Select(m => m.MuscleGroup.Trim()).ToList();
        if (muscleNames.Distinct(StringComparer.OrdinalIgnoreCase).Count() != muscleNames.Count)
        {
            throw new TrainingLogException(TrainingLogErrorType.Validation, "Duplicate muscle groups are not allowed.");
        }

        if (request.Muscles.Any(m => m.Contribution != PrimaryContribution && m.Contribution != SecondaryContribution))
        {
            throw new TrainingLogException(
                TrainingLogErrorType.Validation,
                "Contribution must be 1.0 (primary) or 0.5 (secondary).");
        }

        var nameTaken = await _db.Exercises.AnyAsync(
            e => e.Name.ToLower() == name.ToLower()
                 && (!e.IsCustom || e.CreatedByUserId == userId),
            cancellationToken);

        if (nameTaken)
        {
            throw new TrainingLogException(TrainingLogErrorType.Conflict, "An exercise with the same name already exists.");
        }

        var muscleGroups = await _db.MuscleGroups
            .Where(group => muscleNames.Contains(group.Name))
            .ToListAsync(cancellationToken);
        var muscleGroupByName = muscleGroups.ToDictionary(
            group => group.Name,
            StringComparer.OrdinalIgnoreCase);

        var missing = muscleNames
            .Where(muscleName => !muscleGroupByName.ContainsKey(muscleName))
            .ToList();
        if (missing.Count > 0)
        {
            throw new TrainingLogException(
                TrainingLogErrorType.Validation,
                $"Unknown muscle groups: {string.Join(", ", missing)}.");
        }

        var equipment = request.Equipment.Trim();
        var exercise = new Exercise
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = exerciseType,
            Equipment = equipment,
            IsCustom = true,
            CreatedByUserId = userId,
            WeightStepKg = EquipmentWeightStep.ForEquipment(equipment),
            Muscles = request.Muscles
                .Select(m => new ExerciseMuscle
                {
                    MuscleGroupId = muscleGroupByName[m.MuscleGroup.Trim()].Id,
                    Contribution = m.Contribution
                })
                .ToList()
        };

        _db.Exercises.Add(exercise);
        await _db.SaveChangesAsync(cancellationToken);

        return new ExerciseDto
        {
            Id = exercise.Id,
            Name = exercise.Name,
            Type = exercise.Type.ToString(),
            Equipment = exercise.Equipment,
            IsCustom = true,
            WeightStepKg = exercise.WeightStepKg,
            DefaultWeightStepKg = exercise.WeightStepKg,
            IsWeightStepOverridden = false,
            Muscles = request.Muscles
                .OrderByDescending(m => m.Contribution)
                .Select(m => new MuscleContributionDto
                {
                    MuscleGroup = muscleGroupByName[m.MuscleGroup.Trim()].Name,
                    Contribution = m.Contribution
                })
                .ToList()
        };
    }

    public async Task<IReadOnlyList<string>> GetMuscleGroupNamesAsync(
        CancellationToken cancellationToken = default)
    {
        return await _db.MuscleGroups
            .AsNoTracking()
            .OrderBy(group => group.Name)
            .Select(group => group.Name)
            .ToListAsync(cancellationToken);
    }
}
