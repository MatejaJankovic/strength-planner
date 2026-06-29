using StrengthPlanner.Application.DTOs.Exercises;

namespace StrengthPlanner.Application.Interfaces;

public interface IExerciseService
{
    /// <summary>Sve vežbe sa pripadajućim mišićnim grupama (za katalog/proveru seed-a).</summary>
    Task<IReadOnlyList<ExerciseDto>> GetAllAsync();
}
