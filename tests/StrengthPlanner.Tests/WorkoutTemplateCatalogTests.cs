using StrengthPlanner.Application.Templates;
using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Tests;

/// <summary>
/// Šabloni nisu proizvoljni spiskovi vežbi: ostatak sistema se oslanja na njihov oblik,
/// a propisani volumen mora da stane u pojas koji isti taj sistem uči i nadgleda.
/// </summary>
public class WorkoutTemplateCatalogTests
{
    private static IReadOnlyList<string> ForLevel(WorkoutTemplateDay day, ExperienceLevel level) =>
        SessionComposition.ForLevel(day.Exercises, ExerciseCatalog.IsCompound, level);

    [Fact]
    public void EveryTemplate_OnlyUsesSeededExercises()
    {
        // Generator baca izuzetak na nepoznat naziv tek pri pravljenju plana; ovo hvata
        // grešku u kucanju pre toga.
        foreach (var template in WorkoutTemplateCatalog.GetAll())
        {
            foreach (var day in template.Days)
            {
                foreach (var exercise in day.Exercises)
                {
                    Assert.True(
                        ExerciseCatalog.Find(exercise) is not null,
                        $"{template.Key}/{day.Name}: '{exercise}' nije među seed vežbama.");
                }
            }
        }
    }

    [Fact]
    public void EveryDay_ListsEachExerciseOnlyOnce()
    {
        foreach (var template in WorkoutTemplateCatalog.GetAll())
        {
            foreach (var day in template.Days)
            {
                Assert.Equal(
                    day.Exercises.Count,
                    day.Exercises.Distinct(StringComparer.OrdinalIgnoreCase).Count());
            }
        }
    }

    [Fact]
    public void EveryTemplate_PutsCompoundsBeforeIsolations()
    {
        // SessionComposition bira vežbe po ovom redosledu; ako ga šablon ne poštuje,
        // izbor po nivou iskustva prestaje da ima smisla.
        foreach (var template in WorkoutTemplateCatalog.GetAll())
        {
            foreach (var day in template.Days)
            {
                var names = day.Exercises.ToList();
                var lastCompound = names.FindLastIndex(ExerciseCatalog.IsCompound);
                var firstIsolation = names.FindIndex(name => !ExerciseCatalog.IsCompound(name));

                if (lastCompound >= 0 && firstIsolation >= 0)
                {
                    Assert.True(
                        lastCompound < firstIsolation,
                        $"{template.Key}/{day.Name}: izolacija stoji pre složene vežbe.");
                }
            }
        }
    }

    /// <summary>
    /// Razlog postojanja ove grane. Napredni vežbač sme samo jednu složenu vežbu po
    /// treningu, pa mu je dan sa svega dve izolacije davao trening od tri vežbe.
    /// </summary>
    [Fact]
    public void EveryDay_GivesEveryLevelAFullSession()
    {
        // Pet vežbi je donja granica punog treninga: nijedan dan ni na jednom nivou ne
        // sme da padne ispod nje. Šabloni od pet i šest dana namerno nose četiri
        // izolacije po danu — na toj frekvenciji je manji volumen po treningu poenta —
        // pa napredni tamo dobija pet umesto šest vežbi.
        const int minimumFullSession = 5;

        foreach (var level in Enum.GetValues<ExperienceLevel>())
        {
            var ceiling = ExperienceProgramming.ExercisesPerSession(level);

            foreach (var template in WorkoutTemplateCatalog.GetAll())
            {
                foreach (var day in template.Days)
                {
                    var chosen = ForLevel(day, level);

                    Assert.InRange(chosen.Count, minimumFullSession, ceiling);
                }
            }
        }
    }

    [Fact]
    public void TemplatesOfFourDaysOrFewer_GiveEveryLevelTheFullSessionSize()
    {
        // Do četiri dana nedeljno nema razloga za skraćeni trening.
        foreach (var level in Enum.GetValues<ExperienceLevel>())
        {
            var wanted = ExperienceProgramming.ExercisesPerSession(level);

            foreach (var template in WorkoutTemplateCatalog.GetAll().Where(t => t.Days.Count <= 4))
            {
                foreach (var day in template.Days)
                {
                    var chosen = ForLevel(day, level);

                    Assert.True(
                        chosen.Count == wanted,
                        $"{level} {template.Key}/{day.Name}: {chosen.Count} vežbi umesto {wanted}.");
                }
            }
        }
    }

    [Fact]
    public void EveryDay_RespectsTheCompoundBudgetOfEveryLevel()
    {
        // Popunjavanje treninga ne sme da probije budžet složenih vežbi — a probija ga
        // samo kada dan nema dovoljno izolacija, što ovde više nije slučaj.
        foreach (var level in Enum.GetValues<ExperienceLevel>())
        {
            var budget = ExperienceProgramming.MaxCompoundsPerSession(level);

            foreach (var template in WorkoutTemplateCatalog.GetAll())
            {
                foreach (var day in template.Days)
                {
                    var compounds = ForLevel(day, level).Count(ExerciseCatalog.IsCompound);

                    Assert.True(
                        compounds <= budget,
                        $"{level} {template.Key}/{day.Name}: {compounds} složenih umesto najviše {budget}.");
                }
            }
        }
    }

    /// <summary>
    /// Priručnik naprednom vežbaču daje <i>"do 3 složene vežbe nedeljno"</i>, ali pravilo
    /// koje sistem primenjuje je <b>po treningu</b>, ne po nedelji. Na šest treninga
    /// nedeljno to ispadne šest složenih vežbi. Test pribeležava tu razliku umesto da se
    /// pravi da je nema: napredni vežbač koji hoće da ostane u okviru iz priručnika treba
    /// da bira šablon do tri dana nedeljno.
    /// </summary>
    [Fact]
    public void AdvancedLifter_GetsExactlyOneCompoundPerSession_WhichWeeklyMeansOnePerTrainingDay()
    {
        foreach (var template in WorkoutTemplateCatalog.GetAll())
        {
            var weeklyCompounds = template.Days
                .Sum(day => ForLevel(day, ExperienceLevel.Advanced).Count(ExerciseCatalog.IsCompound));

            Assert.Equal(template.Days.Count, weeklyCompounds);
        }
    }

    [Fact]
    public void EveryTemplate_RotatesTheOpeningCompoundAcrossDays()
    {
        // Napredni vežbač zadržava samo prvu složenu vežbu dana. Šablon koji svaki dan
        // otvara istim pokretom njemu daje nedelju bez ijedne druge složene vežbe.
        foreach (var template in WorkoutTemplateCatalog.GetAll())
        {
            var openers = template.Days
                .Select(day => day.Exercises.First())
                .ToList();

            Assert.Equal(openers.Count, openers.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }
    }

    [Fact]
    public void EveryTemplate_HasUniqueDayLabels()
    {
        // Progresija spaja treninge iz uzastopnih nedelja po nazivu dana; dva ista
        // naziva u istom šablonu bi spojila pogrešne treninge.
        foreach (var template in WorkoutTemplateCatalog.GetAll())
        {
            var labels = template.Days.Select(day => day.Name).ToList();

            Assert.Equal(labels.Count, labels.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }
    }

    [Fact]
    public void EveryTemplate_HasAUniqueKeyAndFitsTheWeek()
    {
        var keys = WorkoutTemplateCatalog.GetAll().Select(template => template.Key).ToList();
        Assert.Equal(keys.Count, keys.Distinct(StringComparer.OrdinalIgnoreCase).Count());

        var names = WorkoutTemplateCatalog.GetAll().Select(template => template.Name).ToList();
        Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());

        foreach (var template in WorkoutTemplateCatalog.GetAll())
        {
            Assert.InRange(template.Days.Count, 1, TrainingWeekSchedule.MaxDaysPerWeek);
        }
    }

    [Fact]
    public void Catalog_CoversEveryTrainingFrequencyFromTwoToSix()
    {
        var offered = WorkoutTemplateCatalog
            .GetAll()
            .Select(template => template.Days.Count)
            .ToHashSet();

        for (var days = 2; days <= 6; days++)
        {
            Assert.Contains(days, offered);
        }
    }

    [Fact]
    public void GetByKey_IsCaseInsensitiveAndReturnsNullForUnknownKeys()
    {
        Assert.NotNull(WorkoutTemplateCatalog.GetByKey("FULL-BODY-4"));
        Assert.Null(WorkoutTemplateCatalog.GetByKey("nope"));
    }

    [Fact]
    public void HigherFrequencyTemplates_CarryFewerCompoundsPerSession()
    {
        // Priručnik: pri većoj frekvenciji "ključno je smanjiti volumen po treningu",
        // jer se ista mišićna grupa pogađa više puta nedeljno.
        var compoundsPerSession = WorkoutTemplateCatalog
            .GetAll()
            .ToDictionary(
                template => template.Key,
                template => template.Days.Max(day => day.Exercises.Count(ExerciseCatalog.IsCompound)));

        Assert.True(
            compoundsPerSession[WorkoutTemplateCatalog.PushPullLegsSixDayKey]
            < compoundsPerSession[WorkoutTemplateCatalog.PushPullLegsKey],
            "Šestodnevni šablon nosi bar onoliko složenih vežbi po treningu koliko i trodnevni.");
    }

    /// <summary>
    /// Najvažniji test u fajlu: nedeljni volumen koji šablon propisuje mora da stane
    /// između MEV i MRV za svaku mišićnu grupu koju uopšte pogađa. Plan koji već na
    /// startu stoji iznad MRV tera sistem u deload pre nego što je išta naučio, a plan
    /// ispod MEV ne stimuliše rast.
    /// </summary>
    [Theory]
    [InlineData(ExperienceLevel.Beginner)]
    [InlineData(ExperienceLevel.Intermediate)]
    [InlineData(ExperienceLevel.Advanced)]
    public void EveryTemplate_StaysUnderMrvForEveryMuscle(ExperienceLevel level)
    {
        // Plan koji već na startu stoji iznad MRV tera sistem u deload pre nego što je
        // išta naučio o korisniku.
        var breaches = Breaches(level, (sets, band) => sets > band.Mrv, "prelazi MRV", band => band.Mrv);

        Assert.True(breaches.Count == 0, string.Join(Environment.NewLine, breaches));
    }

    /// <summary>
    /// Svaki šablon od tri dana naviše mora da dostigne MEV za svaku mišićnu grupu koju
    /// pogađa — plan ispod MEV ne stimuliše rast. Meri se na srednjem nivou, koji je
    /// referenca sistema (neskalirane seed granice, ponašanje nepromenjeno od početka).
    ///
    /// Početnik i napredni vežbač ostaju ispod MEV za pojedine grupe i to nije stvar
    /// šablona nego njihovih konstanti — vidi
    /// <see cref="LevelConstants_CapTheWeekBelowTheAdvancedLifterOwnMev"/>.
    /// </summary>
    [Fact]
    public void TemplatesOfThreeDaysOrMore_ReachMevAtTheReferenceLevel()
    {
        var breaches = Breaches(
            ExperienceLevel.Intermediate,
            (sets, band) => sets < band.Mev,
            "je ispod MEV",
            band => band.Mev,
            template => template.Days.Count >= 3);

        Assert.True(breaches.Count == 0, string.Join(Environment.NewLine, breaches));
    }

    /// <summary>
    /// MEV provera gleda samo grupe koje šablon zaista pogađa, pa bi šablon mogao da je
    /// zaobiđe tako što grupu izostavi u celosti. Ovaj test zatvara tu rupu: velike
    /// grupe mora da pogodi svaki šablon, na svakom nivou.
    ///
    /// Listovi i trbuh nisu na spisku namerno — u trodnevnom rasporedu nema mesta za
    /// njih a da veće grupe ne padnu ispod MEV, i njihov izostanak je manja šteta.
    /// </summary>
    [Fact]
    public void EveryTemplate_TrainsEveryMajorMuscleGroup()
    {
        string[] major =
            ["Chest", "Back", "Shoulders", "Quads", "Hamstrings", "Glutes", "Biceps", "Triceps"];

        foreach (var level in Enum.GetValues<ExperienceLevel>())
        {
            foreach (var template in WorkoutTemplateCatalog.GetAll())
            {
                var trained = WeeklySetsByMuscle(
                    template,
                    level,
                    ExperienceProgramming.StartingSetsPerExercise(level));

                foreach (var muscle in major)
                {
                    Assert.True(
                        trained.ContainsKey(muscle),
                        $"{level} {template.Key}: nijedna vežba ne pogađa {muscle}.");
                }
            }
        }
    }

    /// <summary>
    /// Zabeležena granica sistema, ne šablona.
    ///
    /// Naprednom vežbaču sistem daje 3 serije po vežbi i 6 vežbi po treningu, a njegove
    /// granice volumena množi sa 1.2. Zbir skaliranih MEV vrednosti tada premašuje ono
    /// što nedelja uopšte može da isporuči na manje od šest treninga — nijedan šablon to
    /// ne može da popravi. Ako se konstante nivoa jednog dana usklade, ovaj test će pasti
    /// i treba ga obrisati.
    /// </summary>
    [Fact]
    public void LevelConstants_CapTheWeekBelowTheAdvancedLifterOwnMev()
    {
        const int level3DayTemplateDays = 3;

        var deliverable = level3DayTemplateDays
                          * ExperienceProgramming.ExercisesPerSession(ExperienceLevel.Advanced)
                          * ExperienceProgramming.StartingSetsPerExercise(ExperienceLevel.Advanced);

        var requiredMev = ExerciseCatalog.VolumeLandmarks.Sum(seed =>
            ExperienceProgramming
                .ScaleLandmarks(new VolumeLandmarkValues(seed.Mev, seed.Mav, seed.Mrv), ExperienceLevel.Advanced)
                .Mev);

        Assert.True(
            deliverable < requiredMev,
            $"Napredni nivo sada isporučuje {deliverable} serija nedeljno na tri dana, "
            + $"a zbir njegovih MEV vrednosti je {requiredMev} — ograničenje više ne važi.");
    }

    /// <summary>
    /// Dva treninga nedeljno ne mogu da dostignu MEV za većinu grupa — nema dovoljno
    /// mesta u nedelji. To nije greška šablona nego posledica frekvencije, pa šablon
    /// nosi upozorenje umesto da se pravi da je pun plan. Test postoji da bi ta razlika
    /// ostala namerna: ako neko jednog dana proširi dvodnevni šablon dovoljno da pređe
    /// MEV, upozorenje treba skloniti.
    /// </summary>
    [Fact]
    public void TwoDayTemplate_SitsBelowMevByDesign()
    {
        var twoDay = WorkoutTemplateCatalog.GetByKey(WorkoutTemplateCatalog.FullBodyTwoDayKey)!;

        Assert.False(string.IsNullOrWhiteSpace(twoDay.Note));

        var weeklySets = WeeklySetsByMuscle(
            twoDay,
            ExperienceLevel.Intermediate,
            ExperienceProgramming.StartingSetsPerExercise(ExperienceLevel.Intermediate));

        var belowMev = ExerciseCatalog.VolumeLandmarks
            .Where(seed => weeklySets.TryGetValue(seed.Muscle, out var sets) && sets < seed.Mev)
            .Select(seed => seed.Muscle)
            .ToList();

        Assert.NotEmpty(belowMev);
    }

    [Fact]
    public void EveryTemplateOtherThanTheTwoDayOne_CarriesNoWarning()
    {
        foreach (var template in WorkoutTemplateCatalog.GetAll())
        {
            if (template.Key == WorkoutTemplateCatalog.FullBodyTwoDayKey)
            {
                continue;
            }

            Assert.Null(template.Note);
        }
    }

    private static List<string> Breaches(
        ExperienceLevel level,
        Func<decimal, VolumeLandmarkValues, bool> isBreach,
        string what,
        Func<VolumeLandmarkValues, int> limit,
        Func<WorkoutTemplate, bool>? include = null)
    {
        var setsPerExercise = ExperienceProgramming.StartingSetsPerExercise(level);
        var breaches = new List<string>();

        foreach (var template in WorkoutTemplateCatalog.GetAll().Where(include ?? (_ => true)))
        {
            var weeklySets = WeeklySetsByMuscle(template, level, setsPerExercise);

            foreach (var seed in ExerciseCatalog.VolumeLandmarks)
            {
                if (!weeklySets.TryGetValue(seed.Muscle, out var sets))
                {
                    continue;
                }

                var band = ExperienceProgramming.ScaleLandmarks(
                    new VolumeLandmarkValues(seed.Mev, seed.Mav, seed.Mrv),
                    level);

                if (isBreach(sets, band))
                {
                    breaches.Add($"{level} {template.Key}/{seed.Muscle}: {sets} serija {what} {limit(band)}.");
                }
            }
        }

        return breaches;
    }

    private static Dictionary<string, decimal> WeeklySetsByMuscle(
        WorkoutTemplate template,
        ExperienceLevel level,
        int setsPerExercise)
    {
        var totals = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        foreach (var day in template.Days)
        {
            foreach (var exerciseName in ForLevel(day, level))
            {
                var exercise = ExerciseCatalog.Find(exerciseName)!;

                foreach (var muscle in exercise.Muscles)
                {
                    // Isti obračun koji VolumeService koristi: doprinos puta broj serija.
                    totals[muscle.Muscle] =
                        totals.GetValueOrDefault(muscle.Muscle) + muscle.Contribution * setsPerExercise;
                }
            }
        }

        return totals;
    }
}
