using Microsoft.EntityFrameworkCore;
using StrengthPlanner.Application.DTOs.Exercises;
using StrengthPlanner.Application.Interfaces;
using StrengthPlanner.Infrastructure.Persistence;

namespace StrengthPlanner.Infrastructure.Exercises;

public class ExerciseService : IExerciseService
{
    private readonly AppDbContext _db;

    public ExerciseService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ExerciseDto>> GetAllAsync()
    {
        return await _db.Exercises
            .AsNoTracking()
            .OrderBy(e => e.Name)
            .Select(e => new ExerciseDto
            {
                Id = e.Id,
                Name = e.Name,
                Type = e.Type.ToString(),
                Equipment = e.Equipment,
                IsCustom = e.IsCustom,
                Muscles = e.Muscles
                    .OrderByDescending(m => m.Contribution)
                    .Select(m => new MuscleContributionDto
                    {
                        MuscleGroup = m.MuscleGroup.Name,
                        Contribution = m.Contribution
                    })
                    .ToList()
            })
            .ToListAsync();
    }
}
