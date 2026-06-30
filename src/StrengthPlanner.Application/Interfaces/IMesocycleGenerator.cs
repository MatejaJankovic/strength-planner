using StrengthPlanner.Application.DTOs.Mesocycles;

namespace StrengthPlanner.Application.Interfaces;

public interface IMesocycleGenerator
{
    Task<MesocycleDto> GenerateAsync(
        Guid userId,
        GenerateMesocycleRequest request,
        CancellationToken cancellationToken = default);
}
