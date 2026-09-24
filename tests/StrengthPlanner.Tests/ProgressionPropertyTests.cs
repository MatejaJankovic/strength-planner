using StrengthPlanner.Domain.Algorithms;

namespace StrengthPlanner.Tests;

/// <summary>
/// Svojstva progresije nad mrežom težina, koraka, opsega i ciljnih RIR vrednosti.
///
/// Postoje zato što je greška sa vrhom opsega prolazila testove slučajno: jedini test koji
/// je trebalo da je uhvati koristio je 100 kg, a baš tu je stara formula bila stabilna.
/// Pojedinačni primeri pokrivaju tačke koje je neko izabrao; ovde se proverava cela mreža.
/// </summary>
public class ProgressionPropertyTests
{
    private static readonly decimal[] Steps = [0.5m, 1m, 2m, 2.5m, 5m, 10m];

    private static readonly (int Min, int Max)[] Ranges = [(3, 6), (8, 12), (11, 12), (3, 4), (6, 9)];

    private static readonly decimal[] OffGridWeights = [41.5m, 83.1m, 101m, 126.3m];

    private readonly ProgressionEngine _engine = new();

    [Fact]
    public void TopOfRange_NeverLowersLoad_AndStepsWheneverTheResetCoversTheShortfall()
    {
        var failures = new List<string>();

        foreach (var step in Steps)
        {
            foreach (var used in WeightsFor(step))
            {
                foreach (var (min, max) in Ranges)
                {
                    for (var targetRir = 1; targetRir <= 4; targetRir++)
                    {
                        foreach (var set in TopOfRangeSets(max))
                        {
                            var sets = new[] { set, set, set };
                            var result = _engine.ComputeNext(used, sets, targetRir, min, max, step);
                            var deviation = set.EffectiveRir(min) - targetRir;

                            if (result.NextWeightKg < used)
                            {
                                failures.Add($"lowered {used} -> {result.NextWeightKg} ({Describe(set, min, max, targetRir, step)})");
                            }

                            if (deviation + (max - min) >= 0 && result.NextWeightKg < used + (step / 2))
                            {
                                failures.Add($"no step {used} -> {result.NextWeightKg} ({Describe(set, min, max, targetRir, step)})");
                            }
                        }
                    }
                }
            }
        }

        AssertNone(failures);
    }

    [Fact]
    public void ComputeNext_IsMonotoneInRepsRirAndTheFailureFlag()
    {
        // Više ponavljanja, više RIR-a ili skinuta kvačica otkaza na istoj težini nikad ne
        // sme da da manji predlog. Pokriva i prelaz preko dna opsega, prelaz RIR 0 -> 1
        // (ImpliesFailure se gasi) i prelaz na vrh opsega (menja se grana pravila).
        var failures = new List<string>();

        foreach (var step in new[] { 2m, 2.5m, 5m })
        {
            // I težine van mreže koraka: upotrebljena težina dolazi iz onoga što je vežbač
            // upisao, pa ne mora da bude umnožak koraka. Tu je nemonotonost i bila moguća,
            // dok zaokruživanje nije prestalo da obrće smer korekcije.
            foreach (var used in Enumerable.Range(1, 12).Select(k => k * 7 * step).Concat(OffGridWeights))
            {
                foreach (var (min, max) in Ranges)
                {
                    for (var targetRir = 1; targetRir <= 4; targetRir++)
                    {
                        foreach (var other in OtherSets(min, max, targetRir))
                        {
                            foreach (var set in AllSets(max))
                            {
                                var baseline = Next(used, set, other, min, max, targetRir, step);

                                foreach (var better in Improvements(set, max))
                                {
                                    var improved = Next(used, better, other, min, max, targetRir, step);

                                    if (improved < baseline)
                                    {
                                        failures.Add(
                                            $"{Describe(set, min, max, targetRir, step)} -> {Describe(better, min, max, targetRir, step)} " +
                                            $"at {used} kg: {baseline} -> {improved}");
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        AssertNone(failures);
    }

    [Fact]
    public void ComputeNext_MatchesTheLegacyFormula_WhereNoChangeWasIntended()
    {
        // Izmena je smela da dotakne samo dva slučaja: sesiju na vrhu opsega sa manjkom RIR-a
        // i seriju ispod dna opsega sa rezervom. Svuda drugde mora da da isti broj kao pre.
        var failures = new List<string>();
        var compared = 0;

        foreach (var step in Steps)
        {
            foreach (var used in WeightsFor(step))
            {
                foreach (var (min, max) in Ranges)
                {
                    for (var targetRir = 1; targetRir <= 4; targetRir++)
                    {
                        foreach (var set in AllSets(max))
                        {
                            var sets = new[] { set, set, set };
                            var allHitTop = set.Reps >= max;
                            var belowFloorWithReserve = set.Reps < min
                                                        && !WorkingSet.ImpliesFailure(set.Reps, set.Rir, min, set.IsFailure);
                            var legacyDeviation = LegacyEffectiveRir(set, min) - targetRir;

                            var expected = LegacyNext(used, set, min, max, targetRir, step);

                            // Treći namerno promenjen slučaj: zaokruživanje je znalo da
                            // obrne smer korekcije kad upotrebljena težina nije umnožak
                            // koraka - 41.5 kg je uz +3% davalo 40, a 126.3 kg bez ikakve
                            // korekcije 127.5. Sada takav rezultat ostaje na podignutoj
                            // težini. Vrh opsega ovde ne ulazi: tamo se dodaje korak.
                            var roundingReversedTheCorrection = !allHitTop
                                && (legacyDeviation == 0
                                    ? expected != used
                                    : legacyDeviation < 0 ? expected > used : expected < used);

                            var unchangedCase = !roundingReversedTheCorrection
                                                && (allHitTop
                                                    ? legacyDeviation >= 0
                                                    : !belowFloorWithReserve);

                            if (!unchangedCase)
                            {
                                continue;
                            }

                            compared++;
                            var actual = _engine.ComputeNext(used, sets, targetRir, min, max, step).NextWeightKg;

                            if (actual != expected)
                            {
                                failures.Add($"{Describe(set, min, max, targetRir, step)} at {used} kg: legacy {expected}, now {actual}");
                            }
                        }
                    }
                }
            }
        }

        Assert.True(compared > 100_000, $"The legacy comparison covered only {compared} cases.");
        AssertNone(failures);
    }

    [Fact]
    public void WeightIncreased_ReportsWhetherTheProposalIsHeavier()
    {
        var failures = new List<string>();

        foreach (var step in Steps)
        {
            foreach (var used in WeightsFor(step))
            {
                foreach (var (min, max) in Ranges)
                {
                    for (var targetRir = 1; targetRir <= 4; targetRir++)
                    {
                        foreach (var set in AllSets(max))
                        {
                            var result = _engine.ComputeNext(used, [set, set, set], targetRir, min, max, step);

                            if (result.WeightIncreased != result.NextWeightKg > used)
                            {
                                failures.Add($"{Describe(set, min, max, targetRir, step)} at {used} kg: " +
                                             $"{result.NextWeightKg}, WeightIncreased = {result.WeightIncreased}");
                            }
                        }
                    }
                }
            }
        }

        AssertNone(failures);
    }

    [Fact]
    public void ABodyPortion_ScalesTheWholeLoadAndOnlyMovesTheStepGrid()
    {
        // Vezba sa telesnom masom mora da se ponasa kao ista vezba u kojoj je telo teg:
        // isti propis, ista korekcija, isti korak. Jedina dozvoljena razlika je gde pada
        // mreza koraka, jer se zaokruzuje ono sto ide na pojas, a deo tela nije umnozak
        // koraka. Bez ovoga je 3% korekcije na zgibu sa +10 kg menjalo 0.3 kg.
        var failures = new List<string>();
        var portions = new[] { 51.2m, 68m, 80m };

        foreach (var step in new[] { 1m, 2.5m, 5m })
        {
            foreach (var portion in portions)
            {
                foreach (var added in new[] { 0m, step, 4 * step, 8 * step })
                {
                    foreach (var (min, max) in Ranges)
                    {
                        for (var targetRir = 1; targetRir <= 4; targetRir++)
                        {
                            foreach (var set in AllSets(max))
                            {
                                var sets = new[] { set, set, set };
                                var result = _engine.ComputeNext(
                                    added,
                                    sets,
                                    targetRir,
                                    min,
                                    max,
                                    step,
                                    portion);
                                var described = $"{Describe(set, min, max, targetRir, step)} at {added}+{portion} kg";

                                if (result.NextWeightKg < 0)
                                {
                                    failures.Add($"negative added load {result.NextWeightKg} ({described})");
                                }

                                if (result.LoadFloorReached && result.NextWeightKg != 0)
                                {
                                    failures.Add($"floor reached but proposes {result.NextWeightKg} ({described})");
                                }

                                // Predlog mora da bude umnozak koraka: tanjiri se stavljaju
                                // na pojas, a deo tela (51.2 kg uz korak od 1 kg) nije na
                                // mrezi. Zaokruzivanje nad ukupnim daje 4.8 kg na pojasu.
                                if (result.NextWeightKg % step != 0)
                                {
                                    failures.Add($"off the step grid: {result.NextWeightKg} ({described})");
                                }

                                if (set.Reps >= max && result.NextWeightKg < added)
                                {
                                    failures.Add($"top of range lowered {added} -> {result.NextWeightKg} ({described})");
                                }

                                if (result.LoadFloorReached)
                                {
                                    continue;
                                }

                                // Isti scenario sa telom pretvorenim u teg: ukupno
                                // opterecenje sme da se razlikuje najvise za jedan korak,
                                // koliko nosi zaokruzivanje u dodatom prostoru.
                                var asExternalLoad = _engine.ComputeNext(
                                    added + portion,
                                    sets,
                                    targetRir,
                                    min,
                                    max,
                                    step);
                                var difference = Math.Abs(
                                    (result.NextWeightKg + portion) - asExternalLoad.NextWeightKg);

                                if (difference > step)
                                {
                                    failures.Add(
                                        $"total {result.NextWeightKg + portion} vs {asExternalLoad.NextWeightKg} ({described})");
                                }
                            }
                        }
                    }
                }
            }
        }

        AssertNone(failures);
    }

    private decimal Next(
        decimal used,
        WorkingSet set,
        WorkingSet other,
        int min,
        int max,
        int targetRir,
        decimal step)
    {
        return _engine.ComputeNext(used, [set, other, other], targetRir, min, max, step).NextWeightKg;
    }

    private static IEnumerable<decimal> WeightsFor(decimal step)
    {
        return Enumerable.Range(1, 160).Select(k => k * step).Concat(OffGridWeights);
    }

    /// <summary>Every legal set at the top of the range or two reps past it.</summary>
    private static IEnumerable<WorkingSet> TopOfRangeSets(int max)
    {
        foreach (var reps in new[] { max, max + 2 })
        {
            for (var rir = 0; rir <= 5; rir++)
            {
                yield return new WorkingSet(reps, rir);
            }

            yield return new WorkingSet(reps, 0, IsFailure: true);
        }
    }

    /// <summary>Every legal set from one rep up to two past the top; a failure only at RIR 0.</summary>
    private static IEnumerable<WorkingSet> AllSets(int max)
    {
        for (var reps = 1; reps <= max + 2; reps++)
        {
            for (var rir = 0; rir <= 5; rir++)
            {
                yield return new WorkingSet(reps, rir);
            }

            yield return new WorkingSet(reps, 0, IsFailure: true);
        }
    }

    private static IEnumerable<WorkingSet> OtherSets(int min, int max, int targetRir)
    {
        yield return new WorkingSet(max, targetRir);
        yield return new WorkingSet(max, 0, IsFailure: true);
        yield return new WorkingSet(min, targetRir);
        yield return new WorkingSet(Math.Max(1, min - 2), 0, IsFailure: true);
        yield return new WorkingSet(max + 1, 2);
    }

    private static IEnumerable<WorkingSet> Improvements(WorkingSet set, int max)
    {
        if (set.Reps < max + 2)
        {
            yield return set with { Reps = set.Reps + 1 };
        }

        if (!set.IsFailure && set.Rir < 5)
        {
            yield return set with { Rir = set.Rir + 1 };
        }

        if (set.IsFailure)
        {
            yield return set with { IsFailure = false };
        }
    }

    /// <summary>The rule before this change, kept verbatim as an oracle.</summary>
    private static decimal LegacyNext(decimal used, WorkingSet set, int min, int max, int targetRir, decimal step)
    {
        var deviation = LegacyEffectiveRir(set, min) - targetRir;
        var correction = Math.Clamp(
            deviation * TrainingConstants.RpeCorrectionPerPoint,
            -TrainingConstants.MaxCorrection,
            TrainingConstants.MaxCorrection);
        var adjusted = used * (1 + correction);
        var next = set.Reps >= max ? adjusted + step : adjusted;

        return WeightMath.RoundToStep(next, step);
    }

    private static int LegacyEffectiveRir(WorkingSet set, int min)
    {
        return WorkingSet.ImpliesFailure(set.Reps, set.Rir, min, set.IsFailure)
            ? -Math.Max(0, min - set.Reps)
            : set.Rir;
    }

    private static string Describe(WorkingSet set, int min, int max, int targetRir, decimal step)
    {
        var failure = set.IsFailure ? " failure" : string.Empty;
        return $"{set.Reps}@RIR{set.Rir}{failure} in {min}-{max}@{targetRir}, step {step}";
    }

    private static void AssertNone(IReadOnlyCollection<string> failures)
    {
        Assert.True(
            failures.Count == 0,
            $"{failures.Count} violations, first ones:{Environment.NewLine}" +
            string.Join(Environment.NewLine, failures.Take(10)));
    }
}
