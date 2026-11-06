using System.Globalization;
using System.Text;
using System.Text.Json;
using StrengthPlanner.Domain.Algorithms;

namespace StrengthPlanner.Tests;

/// <summary>
/// Ekran treninga računa cilj ponavljanja i poruku posle serije sam, iz težine koja se upravo
/// unosi, pa drži kopiju pravila <see cref="StepAbsorption"/> (`step-absorption.ts`). Dve kopije
/// jednog pravila se tiho razilaze, a tabela koja ih je držala zajedno bila je ručno prepisana,
/// sa dvanaest slučajeva.
///
/// Sada obe strane proveravaju isti fajl, `step-absorption.cases.json`, sa tri tabele: cilj
/// ponavljanja (i za težine van mreže koraka i za vežbe sa telesnom masom), upijanje koraka
/// (rezerva 0-8, ukupno opterećenje na dve decimale) i sledeća težina na stalku (koraci
/// 0.5-10). Kad se pravilo promeni na serveru, pada ovaj test; fajl se tada generiše iznova
/// (vidi <see cref="Regenerate"/>), i klijent mora da ga prođe.
/// </summary>
public class StepAbsorptionSharedCasesTests
{
    private const string FileName = "step-absorption.cases.json";
    private const string SourcePath = "strength-planner-web/src/app/features/workout/" + FileName;

    private sealed record SharedCases(decimal[][] RepsToEarnStep, decimal[][] Absorbs, decimal[][] StepAbove);

    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [Fact]
    public void TheServerRule_MatchesEveryCaseTheClientIsTestedAgainst()
    {
        // Fajl se kopira uz test (StrengthPlanner.Tests.csproj), pa se čita iz izlaza, a ne
        // traži penjanjem po folderima: worktree bez fajla bi tako tiho pročitao tuđu kopiju.
        var path = Path.Combine(AppContext.BaseDirectory, FileName);
        var cases = JsonSerializer.Deserialize<SharedCases>(File.ReadAllText(path), Json)!;
        var failures = new List<string>();

        foreach (var c in cases.RepsToEarnStep)
        {
            var reps = StepAbsorption.RepsToEarnStep(c[0], c[1], c[2], (int)c[3], (int)c[4], (int)c[5]);
            if (reps != (int)c[6])
            {
                failures.Add($"cilj {Format(c)}: server {reps}");
            }
        }

        foreach (var c in cases.Absorbs)
        {
            var absorbs = StepAbsorption.Absorbs(c[0], c[1], (int)c[2], (int)c[3], (int)c[4]);
            if (absorbs != (c[5] == 1))
            {
                failures.Add($"upija {Format(c)}: server {absorbs}");
            }
        }

        foreach (var c in cases.StepAbove)
        {
            var next = WeightMath.StepAbove(c[0], c[1]);
            if (next != c[2])
            {
                failures.Add($"sledeća {Format(c)}: server {next}");
            }
        }

        Assert.True(cases.RepsToEarnStep.Length > 1000 && cases.Absorbs.Length > 500 && cases.StepAbove.Length > 40);
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures.Take(20)));
    }

    /// <summary>
    /// Cilj na kartici je obećanje: kad sve serije stignu do njega uz ciljnu rezervu, sledeći
    /// put dolazi korak. Ovde se obećanje proverava nad samom progresijom, i na težinama van
    /// mreže koraka. Serverski <see cref="StepAbsorption.RepsToEarnStep(decimal, decimal, decimal, int, int, int)"/>
    /// nema poziv u produkciji, pa je bez ovoga mogao da se razlikuje od onoga što progresija
    /// radi, a da to nijedan test ne vidi: 9 kg na koraku od 2 kg je tražilo 16 ponavljanja, a
    /// progresija je dizala već posle 12 (pregled #108).
    /// </summary>
    [Fact]
    public void TheTargetOnTheCard_IsWhereProgressionTakesTheStep()
    {
        var engine = new ProgressionEngine();
        var failures = new List<string>();

        foreach (var step in new[] { 2m, 2.5m, 5m })
        foreach (var added in Enumerable.Range(1, 16).Select(k => k * step).Concat(Enumerable.Range(1, 8).Select(k => (k + 0.5m) * step)))
        foreach (var (min, max) in new[] { (8, 12), (10, 20), (3, 6) })
        for (var rir = 1; rir <= 3; rir++)
        {
            var target = StepAbsorption.RepsToEarnStep(added, 0m, step, min, max, rir);
            for (var reps = max; reps <= target + 2; reps++)
            {
                var sets = Enumerable.Repeat(new WorkingSet(reps, rir), 3).ToList();
                var stepped = engine.ComputeNext(added, sets, rir, min, max, step).NextWeightKg > added;
                if (stepped != (reps >= target))
                {
                    failures.Add($"{added} kg, korak {step}, {min}-{max} @RIR{rir}, 3 x {reps}: cilj {target}, progresija {(stepped ? "diže" : "ne diže")}");
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures.Take(20)));
    }

    /// <summary>
    /// Piše fajl iz serverskog pravila. Radi samo uz promenljivu okruženja, da običan
    /// `dotnet test` ne bi menjao izvorni kod:
    /// <code>STEP_CASES_REGENERATE=1 dotnet test --filter Regenerate</code>
    /// </summary>
    [Fact]
    public void Regenerate()
    {
        if (Environment.GetEnvironmentVariable("STEP_CASES_REGENERATE") != "1")
        {
            return;
        }

        var reps = new List<string>();
        var ranges = new[] { (3, 6), (5, 5), (8, 12), (10, 20), (11, 12) };
        foreach (var step in new[] { 1.25m, 2m, 2.5m, 5m })
        foreach (var added in Enumerable.Range(1, 20).Select(k => k * step)
                     .Concat(Enumerable.Range(0, 12).Select(k => (k + 0.5m) * step)))
        foreach (var body in new[] { 0m, 51.2m })
        foreach (var (min, max) in ranges)
        for (var rir = 1; rir <= 3; rir++)
        {
            var target = StepAbsorption.RepsToEarnStep(added, body, step, min, max, rir);
            reps.Add(Row(added, body, step, min, max, rir, target));
        }

        // Težine na kojima se klijent, koji računa u pokretnom zarezu, slaže sa serverom samo
        // zahvaljujući toleranciji EPSILON. Nađene pretragom svih težina na dve decimale do 25 kg.
        foreach (var (added, step) in new[] { (2.28m, 1m), (4.56m, 2m), (3.8m, 1m) })
        foreach (var body in new[] { 0m, 51.2m })
        foreach (var (min, max) in ranges)
        for (var rir = 1; rir <= 3; rir++)
        {
            var target = StepAbsorption.RepsToEarnStep(added, body, step, min, max, rir);
            reps.Add(Row(added, body, step, min, max, rir, target));
        }

        var absorbs = new List<string>();
        foreach (var step in new[] { 1m, 2m, 2.5m })
        foreach (var added in new[] { 3.8m, 8m, 9m, 15m, 22.5m })
        foreach (var body in new[] { 0m, 51.2m, 68.37m })
        foreach (var min in new[] { 8, 10 })
        foreach (var setReps in new[] { 12, 17, 25 })
        foreach (var rir in new[] { 0, 3, 5, 8 })
        {
            var climb = WeightMath.StepAbove(added, step) - added;
            var result = StepAbsorption.Absorbs(added + body, climb, min, setReps, rir);
            absorbs.Add(Row(added + body, climb, min, setReps, rir, result ? 1 : 0));
        }

        var stepAbove = new List<string>();
        foreach (var weight in new[] { 0m, 3.8m, 9m, 10m, 15m, 22.5m, 22.6m, 100m })
        foreach (var step in new[] { 0.5m, 1m, 1.25m, 2m, 2.5m, 5m, 10m })
        {
            stepAbove.Add(Row(weight, step, WeightMath.StepAbove(weight, step)));
        }

        var json = new StringBuilder("{\n");
        json.Append("  \"repsToEarnStep\": [\n    ").Append(string.Join(",\n    ", reps)).Append("\n  ],\n");
        json.Append("  \"absorbs\": [\n    ").Append(string.Join(",\n    ", absorbs)).Append("\n  ],\n");
        json.Append("  \"stepAbove\": [\n    ").Append(string.Join(",\n    ", stepAbove)).Append("\n  ]\n}\n");
        File.WriteAllText(LocateSource(), json.ToString());
    }

    private static string Row(params decimal[] values)
    {
        return "[" + string.Join(",", values.Select(value => value.ToString(CultureInfo.InvariantCulture))) + "]";
    }

    private static string Format(decimal[] values)
    {
        return string.Join(" ", values.Select(value => value.ToString(CultureInfo.InvariantCulture)));
    }

    private static string LocateSource()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, SourcePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"{SourcePath} nije pronađen iznad {AppContext.BaseDirectory}.");
    }
}
