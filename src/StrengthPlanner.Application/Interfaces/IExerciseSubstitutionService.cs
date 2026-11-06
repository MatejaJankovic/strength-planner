using StrengthPlanner.Application.DTOs.Sessions;

namespace StrengthPlanner.Application.Interfaces;

/// <summary>
/// Zamena vežbe za ostatak bloka: ista vežba na istom danu, u svakom treningu koji još čeka.
/// </summary>
public interface IExerciseSubstitutionService
{
    /// <summary>Vežbe koje smeju da zamene planiranu (<c>ExerciseSubstitution</c>).</summary>
    Task<IReadOnlyList<ExerciseAlternativeDto>> GetAlternativesAsync(
        Guid userId,
        Guid exercisePlanId,
        CancellationToken cancellationToken = default);

    /// <summary>Menja vežbu i vraća id treninga iz koga je zamena tražena.</summary>
    Task<Guid> SubstituteAsync(
        Guid userId,
        Guid exercisePlanId,
        Guid exerciseId,
        CancellationToken cancellationToken = default);
}
