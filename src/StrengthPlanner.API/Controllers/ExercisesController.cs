using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StrengthPlanner.Application.DTOs.Exercises;
using StrengthPlanner.Application.Interfaces;

namespace StrengthPlanner.API.Controllers;

[Authorize]
[ApiController]
[Route("api/exercises")]
public class ExercisesController : AuthorizedControllerBase
{
    private readonly IExerciseService _exerciseService;

    public ExercisesController(IExerciseService exerciseService)
    {
        _exerciseService = exerciseService;
    }

    /// <summary>Katalog vežbi vidljivih korisniku (sistemske + njegove custom).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ExerciseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var exercises = await _exerciseService.GetAllAsync(GetUserId(), cancellationToken);
        return Ok(exercises);
    }

    /// <summary>Kreira korisničku (custom) vežbu.</summary>
    /// <remarks>
    /// Primer body-ja:
    /// { "name": "Hack Squat", "type": "Compound", "equipment": "Machine",
    ///   "muscles": [ { "muscleGroup": "Quads", "contribution": 1.0 },
    ///                { "muscleGroup": "Glutes", "contribution": 0.5 } ] }
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(typeof(ExerciseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateCustom(
        CreateExerciseRequest request,
        CancellationToken cancellationToken)
    {
        var exercise = await _exerciseService.CreateCustomAsync(GetUserId(), request, cancellationToken);
        return CreatedAtAction(nameof(GetAll), new { }, exercise);
    }

    /// <summary>Nazivi mišićnih grupa (za formu dodavanja vežbe).</summary>
    [HttpGet("muscle-groups")]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMuscleGroups(CancellationToken cancellationToken)
    {
        var muscleGroups = await _exerciseService.GetMuscleGroupNamesAsync(cancellationToken);
        return Ok(muscleGroups);
    }
}
