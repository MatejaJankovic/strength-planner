using StrengthPlanner.Application.Templates;
using StrengthPlanner.Domain.Algorithms;

namespace StrengthPlanner.Tests;

/// <summary>
/// Leđa po glavama (R20): latovi i gornja leđa umesto jedne zbirne grupe, kao što je rame podeljeno na tri glave. Podela
/// je odluka o modelu, a ne samo o podacima: svaka vežba ima jednu glavu kao primarnu, a ono što je ranije punilo
/// zbirnu grupu iz sporednog dela (veslanje za latove, zgib za gornja leđa) namerno ne ulazi nigde, jer granice po
/// glavi važe za direktan rad.
/// </summary>
public class BackHeadsTests
{
    private static IReadOnlyList<(string Muscle, decimal Contribution)> BackCredits(string exercise)
    {
        return ExerciseCatalog.Find(exercise)!.Muscles
            .Where(muscle => muscle.Muscle is ExerciseCatalog.Lats or ExerciseCatalog.UpperBack)
            .Select(muscle => (muscle.Muscle, muscle.Contribution))
            .ToList();
    }

    /// <summary>
    /// Migracija SplitBackHeads prepisuje naučene granice zbirne grupe "Back" na obe glave kao polovinu pomaka od
    /// seed-a, sa seed-om glave kao osnovom (5/9/15). Sa drugim seed-om bi isti broj značio nešto drugo, pa seed-ovi
    /// ovde i u migraciji moraju da se poklope. MRV je 15 po glavi (RP: oko 30 serija pri četiri treninga leđa,
    /// otprilike pola vertikalnog, pola veslanja), MEV i MAV su polovina zbirne grupe (10/18).
    /// </summary>
    [Fact]
    public void TheHeadsHaveTheSeedsTheMigrationAssumes()
    {
        var lats = ExerciseCatalog.VolumeLandmarks.Single(seed => seed.Muscle == ExerciseCatalog.Lats);
        var upperBack = ExerciseCatalog.VolumeLandmarks.Single(seed => seed.Muscle == ExerciseCatalog.UpperBack);

        Assert.Equal((5, 9, 15), (lats.Mev, lats.Mav, lats.Mrv));
        Assert.Equal((5, 9, 15), (upperBack.Mev, upperBack.Mav, upperBack.Mrv));
    }

    [Theory]
    [InlineData("Pull-up")]
    [InlineData("Lat Pulldown")]
    [InlineData("Straight-Arm Pulldown")]
    public void VerticalPulls_AreDirectWorkForTheLatsOnly(string exercise)
    {
        Assert.Equal([(ExerciseCatalog.Lats, 1.0m)], BackCredits(exercise));
    }

    [Theory]
    [InlineData("Barbell Row")]
    [InlineData("Seated Cable Row")]
    public void Rows_AreDirectWorkForTheUpperBackOnly(string exercise)
    {
        Assert.Equal([(ExerciseCatalog.UpperBack, 1.0m)], BackCredits(exercise));
    }

    /// <summary>
    /// Mrtvo dizanje, rumunsko i Face Pull daju gornjim leđima pola serije, isto što su davali zbirnoj grupi: debljina
    /// (opružači, trapezi), a ne širina. Opružači nisu zasebna glava: za njih nema izvora granica.
    /// </summary>
    [Theory]
    [InlineData("Deadlift")]
    [InlineData("Romanian Deadlift")]
    [InlineData("Face Pull")]
    public void HingesAndFacePull_CreditHalfASetToTheUpperBack(string exercise)
    {
        Assert.Equal([(ExerciseCatalog.UpperBack, 0.5m)], BackCredits(exercise));
    }

    /// <summary>
    /// Koliko je nedelja (šablon × nivo × cilj × model × nedelja, posle balansiranja) svaka glava leđa ispod svog MEV-a,
    /// po šablonu. Brojevi su zabeleženi tačno i isti su kao u <c>docs/features/back-heads.md</c>: zbirna grupa leđa je
    /// ovo krila, a dve izmene šablona (Push/Pull/Legs: Lat Pulldown umesto drugog veslanja; Legs Specialization:
    /// drugo veslanje na Upper B) bez ovoga nisu imale nijedan test koji bi pao kad se vrate (izmereno: 0).
    /// </summary>
    [Fact]
    public void TheShortfallOfEachBackHead_IsRecordedPerTemplate()
    {
        var shortfall = new SortedDictionary<string, string>();

        foreach (var week in TemplateWeekSimulation.EveryTrainingWeek())
        {
            foreach (var head in new[] { ExerciseCatalog.Lats, ExerciseCatalog.UpperBack })
            {
                var seed = ExerciseCatalog.VolumeLandmarks.Single(landmark => landmark.Muscle == head);
                var mev = ExperienceProgramming.ScaleLandmarks(
                    new VolumeLandmarkValues(seed.Mev, seed.Mav, seed.Mrv),
                    week.Level).Mev;

                if (week.Weekly(head) < mev)
                {
                    var key = $"{week.Template.Key}/{head}";
                    shortfall[key] = (int.Parse(shortfall.GetValueOrDefault(key, "0")) + 1).ToString();
                }
            }
        }

        Assert.Equal(
            new SortedDictionary<string, string>
            {
                ["full-body-2/Lats"] = "21",
                ["full-body-2/Upper Back"] = "76",
                ["full-body-4/Upper Back"] = "4",
                ["full-body/Upper Back"] = "37",
                ["legs-specialization/Upper Back"] = "18",
                ["push-pull-legs-6/Upper Back"] = "4",
                ["push-pull-legs/Lats"] = "49",
                ["upper-lower-ppl/Upper Back"] = "18",
                ["upper-lower-x3/Upper Back"] = "4",
                ["upper-lower/Lats"] = "13",
                ["upper-lower/Upper Back"] = "4",
            }.Select(entry => $"{entry.Key}: {entry.Value}"),
            shortfall.Select(entry => $"{entry.Key}: {entry.Value}"));
    }

    /// <summary>
    /// Zbirne grupe više nema, i nijedna vežba je ne sme pominjati: migracija je briše, a svaki preostali red bi
    /// pokazivao na grupu koja ne postoji.
    /// </summary>
    [Fact]
    public void TheOldAggregateIsGone()
    {
        Assert.DoesNotContain("Back", ExerciseCatalog.MuscleGroupNames);
        Assert.DoesNotContain(ExerciseCatalog.VolumeLandmarks, seed => seed.Muscle == "Back");
        Assert.DoesNotContain(
            ExerciseCatalog.Exercises,
            exercise => exercise.Muscles.Any(muscle => muscle.Muscle == "Back"));
    }

    /// <summary>
    /// Svaka vežba koja ranije jeste radila leđa i dalje ih radi, i to onoliko koliko je ranije. Zbir primarnih udela
    /// (1.0) po vežbi se čuva, pa podela ne menja koliko serija neka vežba broji za leđa, nego samo kojoj glavi.
    /// </summary>
    [Fact]
    public void EveryExerciseThatTrainedTheBack_StillCreditsItsFullContribution()
    {
        var expected = new Dictionary<string, decimal>
        {
            ["Pull-up"] = 1.0m,
            ["Lat Pulldown"] = 1.0m,
            ["Straight-Arm Pulldown"] = 1.0m,
            ["Barbell Row"] = 1.0m,
            ["Seated Cable Row"] = 1.0m,
            ["Deadlift"] = 0.5m,
            ["Romanian Deadlift"] = 0.5m,
            ["Face Pull"] = 0.5m,
        };

        var actual = ExerciseCatalog.Exercises
            .Select(exercise => (exercise.Name, Total: BackCredits(exercise.Name).Sum(credit => credit.Contribution)))
            .Where(item => item.Total > 0)
            .ToDictionary(item => item.Name, item => item.Total);

        Assert.Equal(expected.OrderBy(pair => pair.Key), actual.OrderBy(pair => pair.Key));
    }
}
