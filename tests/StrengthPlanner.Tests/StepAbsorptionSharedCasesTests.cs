using System.Text.Json;
using StrengthPlanner.Domain.Algorithms;

namespace StrengthPlanner.Tests;

/// <summary>
/// Ekran treninga računa cilj ponavljanja sam, iz težine koja se upravo unosi, pa drži kopiju
/// pravila <see cref="StepAbsorption"/> (`step-absorption.ts`). Kopija postoji namerno: server
/// bi morao da odgovara na svako kucanje. Ali dve kopije jednog pravila se tiho razilaze, a
/// tabela koja ih je držala zajedno bila je ručno prepisana, sa dvanaest slučajeva.
///
/// Sada obe strane čitaju isti fajl, `step-absorption.cases.json`, sa 3328 slučajeva (koraci
/// 1, 2, 2.5 i 5 kg, osam opsega, ciljni RIR 1-4). Ako se pravilo promeni na jednoj strani,
/// pada test na drugoj; tada se fajl generiše iznova iz servera, i obe implementacije moraju
/// da ga prođu.
/// </summary>
public class StepAbsorptionSharedCasesTests
{
    private const string CasesPath = "strength-planner-web/src/app/features/workout/step-absorption.cases.json";

    [Fact]
    public void TheServerRule_MatchesEveryCaseTheClientIsTestedAgainst()
    {
        var cases = JsonSerializer.Deserialize<decimal[][]>(File.ReadAllText(LocateCases()))!;
        var failures = new List<string>();

        foreach (var entry in cases)
        {
            var reps = StepAbsorption.RepsToEarnStep(entry[0], entry[1], (int)entry[2], (int)entry[3], (int)entry[4]);
            if (reps != (int)entry[5])
            {
                failures.Add($"{entry[0]} kg, korak {entry[1]}, {entry[2]}-{entry[3]} @RIR{entry[4]}: {reps}, a fajl kaže {entry[5]}");
            }
        }

        Assert.True(cases.Length > 3000, $"Fajl ima samo {cases.Length} slučajeva.");
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures.Take(20)));
    }

    private static string LocateCases()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, CasesPath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"{CasesPath} nije pronađen iznad {AppContext.BaseDirectory}.");
    }
}
