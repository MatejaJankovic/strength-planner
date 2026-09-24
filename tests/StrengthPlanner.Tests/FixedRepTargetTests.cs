using System.ComponentModel.DataAnnotations;
using StrengthPlanner.Application.DTOs.Templates;
using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Tests;

/// <summary>
/// Lični šablon sme da propiše <b>tačan</b> broj ponavljanja, a ne samo opseg.
///
/// 5×5 je program, ne nemarno unet opseg. Provera unosa je to i puštala — odbijala je samo
/// donju granicu iznad gornje — ali je <c>Periodization.ForWeek</c> gornju granicu dizala
/// na <c>min + 1</c>, pa je uneto 5–5 u planu ispadalo 5–6 (isto 12–12 → 11–12, 8–8 → 8–9).
/// Korisnik je unosio jedno, a dobijao drugo, bez ijedne poruke o tome.
/// </summary>
public class FixedRepTargetTests
{
    private const int Sets = 4;

    [Theory]
    [InlineData(PeriodizationModel.Flat)]
    [InlineData(PeriodizationModel.Linear)]
    [InlineData(PeriodizationModel.Inverse)]
    public void AFixedRepTarget_StaysFixedThroughTheWholeBlock(PeriodizationModel model)
    {
        var weeks = Periodization.ForBlock(model, 5, 5, 1, Sets);

        Assert.All(weeks, week => Assert.Equal(week.RepRangeMin, week.RepRangeMax));
    }

    /// <summary>
    /// Fiksan broj se i pomera kao celina: periodizacija ga menja, ali ga ne razvlači u
    /// opseg. Pre ove izmene je linearni blok od 5–5 davao 8–9, 8–9, 5–6, 4–5, 3–4.
    /// </summary>
    [Fact]
    public void AFixedRepTarget_MovesWithTheBlock_WithoutTurningIntoARange()
    {
        var weeks = Periodization.ForBlock(PeriodizationModel.Linear, 5, 5, 1, Sets);

        Assert.Equal(
            new[] { "8-8", "8-8", "5-5", "4-4", "3-3", "5-5" },
            weeks.Select(week => $"{week.RepRangeMin}-{week.RepRangeMax}"));

        // Deload vraća osnovu, pa je i on fiksan.
        Assert.True(weeks[^1].IsDeload);
    }

    /// <summary>
    /// Fiksan broj ponavljanja ostaje unutar granica koje obrazac unosa dozvoljava, i to
    /// je jedini razlog zbog koga ovo pravilo sme da postoji: opseg iznad
    /// <see cref="Periodization.MaxReps"/> bi propis tiho svukao nazad.
    /// </summary>
    [Fact]
    public void TheFormAccepts_AFixedRepTarget()
    {
        var exercise = new SaveCustomTemplateExerciseDto
        {
            ExerciseId = Guid.NewGuid(),
            Sets = 5,
            RepRangeMin = 5,
            RepRangeMax = 5
        };

        var results = new List<ValidationResult>();
        var valid = Validator.TryValidateObject(
            exercise,
            new ValidationContext(exercise),
            results,
            validateAllProperties: true);

        Assert.True(valid, string.Join("; ", results.Select(result => result.ErrorMessage)));
    }

    /// <summary>
    /// Dupla progresija nad fiksnim brojem je klasičan 5×5: pet ponavljanja na ciljnom
    /// RIR-u nosi korak više, a ista serija sa jednim ponavljanjem rezerve manje ne nosi.
    ///
    /// Uslov je isti kao i za opseg (<c>deviation + (max - min) >= 0</c>), samo je ovde
    /// <c>max - min</c> nula, pa ceo teret pada na sam RIR. Zato fiksan broj ponavljanja
    /// i nije "uzak opseg" po nesreći — takav program tako i radi.
    /// </summary>
    [Fact]
    public void TopOfAFixedRepTarget_StepsUpAtTheTargetRir_AndHoldsBelowIt()
    {
        var engine = new ProgressionEngine();

        var atTarget = engine.ComputeNext(
            usedWeightKg: 100m,
            workingSets: [new WorkingSet(5, 2), new WorkingSet(5, 2), new WorkingSet(5, 2)],
            targetRir: 2,
            repRangeMin: 5,
            repRangeMax: 5);

        Assert.Equal(102.5m, atTarget.NextWeightKg);
        Assert.True(atTarget.WeightIncreased);

        var harderThanPlanned = engine.ComputeNext(
            usedWeightKg: 100m,
            workingSets: [new WorkingSet(5, 1), new WorkingSet(5, 1), new WorkingSet(5, 1)],
            targetRir: 2,
            repRangeMin: 5,
            repRangeMax: 5);

        Assert.Equal(100m, harderThanPlanned.NextWeightKg);
        Assert.False(harderThanPlanned.WeightIncreased);
    }

    /// <summary>
    /// Sledeći propis ponavljanja je i dalje dno opsega, što je kod fiksnog broja isti
    /// broj — dakle nema "vrati se na dno" koraka koji bi program pretvorio u nešto drugo.
    /// </summary>
    [Fact]
    public void AFixedRepTarget_PrescribesTheSameRepsNextTime()
    {
        var result = new ProgressionEngine().ComputeNext(
            usedWeightKg: 100m,
            workingSets: [new WorkingSet(5, 2)],
            targetRir: 2,
            repRangeMin: 5,
            repRangeMax: 5);

        Assert.Equal(5, result.NextTargetReps);
    }
}
