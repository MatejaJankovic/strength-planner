namespace StrengthPlanner.Application.DTOs.Exercises;

/// <summary>
/// Postavlja korisnički korak opterećenja za vežbu. Null vraća vežbu na
/// podrazumevani korak izveden iz sprave (briše override).
/// </summary>
public class UpdateWeightStepRequest
{
    public decimal? WeightStepKg { get; set; }
}
