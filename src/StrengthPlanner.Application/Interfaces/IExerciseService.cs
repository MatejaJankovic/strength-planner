using StrengthPlanner.Application.DTOs.Exercises;

namespace StrengthPlanner.Application.Interfaces;

public interface IExerciseService
{
    /// <summary>
    /// Sve vežbe vidljive korisniku: sistemske + njegove custom vežbe
    /// (tuđe custom vežbe se ne vraćaju).
    /// </summary>
    Task<IReadOnlyList<ExerciseDto>> GetAllAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Kreira korisničku (custom) vežbu sa doprinosima mišićnim grupama.</summary>
    Task<ExerciseDto> CreateCustomAsync(Guid userId, CreateExerciseRequest request, CancellationToken cancellationToken = default);

    /// <summary>Nazivi svih mišićnih grupa (za formu dodavanja vežbe).</summary>
    Task<IReadOnlyList<string>> GetMuscleGroupNamesAsync(CancellationToken cancellationToken = default);
}
