using StrengthPlanner.Application.DTOs.Sessions;

namespace StrengthPlanner.Application.Interfaces;

/// <summary>
/// Izmena bloka koji već teče: vežba se dodaje ili uklanja na istom danu, u svakom treningu koji
/// još čeka, od izabranog do kraja bloka. Prošlost se ne dira.
/// </summary>
public interface IBlockEditService
{
    /// <summary>Vežbe koje smeju da se dodaju u dan ovog treninga, ili razlog zbog koga nijedna ne sme.</summary>
    Task<AddableExercisesDto> GetAddableAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>Dodaje vežbu i vraća id treninga iz koga je dodata.</summary>
    Task<Guid> AddExerciseAsync(
        Guid userId,
        Guid sessionId,
        Guid exerciseId,
        CancellationToken cancellationToken = default);

    /// <summary>Uklanja vežbu i vraća id treninga iz koga je uklonjena.</summary>
    Task<Guid> RemoveExerciseAsync(
        Guid userId,
        Guid exercisePlanId,
        CancellationToken cancellationToken = default);
}
