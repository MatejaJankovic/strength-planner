namespace StrengthPlanner.Application.DTOs.Templates;

/// <summary>
/// Šablon onako kako ga vidi konkretan korisnik: dani su već skraćeni na njegov nivo
/// iskustva, pa ono što piše u čarobnjaku odgovara planu koji će dobiti.
/// </summary>
public sealed class WorkoutTemplateDto
{
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Šablon koji je korisnik sam sastavio. Ekran ga prikazuje odvojeno, a i ponaša se
    /// drugačije: ne skraćuje se na nivo iskustva i nosi svoje serije i ponavljanja.
    /// </summary>
    public bool IsCustom { get; set; }

    /// <summary>Upozorenje o poznatom ograničenju šablona, ako ga ima.</summary>
    public string? Note { get; set; }

    public IReadOnlyList<WorkoutTemplateDayDto> Days { get; set; } = [];
}

public sealed class WorkoutTemplateDayDto
{
    public string Name { get; set; } = string.Empty;

    public IReadOnlyList<string> Exercises { get; set; } = [];
}
