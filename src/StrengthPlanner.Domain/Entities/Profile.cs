using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Domain.Entities;

/// <summary>
/// Antropometrijski i trenažni podaci korisnika (1:1 sa <see cref="User"/>).
/// </summary>
public class Profile
{
    public Guid Id { get; set; }

    // FK ka Identity nalogu (ApplicationUser živi u Infrastructure sloju).
    public Guid UserId { get; set; }

    public Sex? Sex { get; set; }
    public int Age { get; set; }
    public decimal BodyweightKg { get; set; }
    public ExperienceLevel ExperienceLevel { get; set; }
}
