using StrengthPlanner.Application.Templates;

namespace StrengthPlanner.Tests;

/// <summary>
/// Uputstvo i katalog šablona moraju da broje isto.
///
/// Nalaz D22 iz pregleda: `HowToUseApp.md` je tvrdilo „Sedam ugrađenih" i nabrajalo sedam,
/// a katalog ih je imao **devet**. Dva šablona dodata u kasnijim rundama nikada nisu stigla
/// do uputstva — i to je vrsta greške koju niko ne primeti, jer i broj i tabela deluju
/// uredno sami za sebe.
///
/// Test čita uputstvo iz repozitorijuma. Kada fajl nije dostupan (spakovan izlaz van
/// radnog stabla), ćuti umesto da padne: nema šta da uporedi, a lažno crveno bi bilo gore
/// od izostale provere.
/// </summary>
public class GuideTemplateTableTests
{
    private static string? FindGuide()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "HowToUseApp.md");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    [Fact]
    public void TheGuideNamesEveryBuiltInTemplate()
    {
        var path = FindGuide();
        if (path is null)
        {
            return;
        }

        var guide = File.ReadAllText(path);

        foreach (var template in WorkoutTemplateCatalog.GetAll())
        {
            Assert.True(
                guide.Contains(template.Name, StringComparison.Ordinal),
                $"HowToUseApp.md ne pominje šablon „{template.Name}“. " +
                "Dodavanje šablona u katalog znači i red u tabeli u uputstvu.");
        }
    }

    /// <summary>
    /// I broj napisan rečju. Tabela može biti potpuna a rečenica iznad nje zastarela —
    /// tačno tako je „Sedam" preživelo dva dodata šablona.
    /// </summary>
    [Fact]
    public void TheGuideCountsTemplatesCorrectly()
    {
        var path = FindGuide();
        if (path is null)
        {
            return;
        }

        var guide = File.ReadAllText(path);
        var written = new Dictionary<int, string>
        {
            [2] = "Dva",
            [3] = "Tri",
            [4] = "Četiri",
            [5] = "Pet",
            [6] = "Šest",
            [7] = "Sedam",
            [8] = "Osam",
            [9] = "Devet",
            [10] = "Deset",
            [11] = "Jedanaest",
            [12] = "Dvanaest"
        };

        var count = WorkoutTemplateCatalog.GetAll().Count;
        Assert.True(written.ContainsKey(count), $"Broj {count} nije u spisku brojeva rečima.");

        Assert.Contains($"{written[count]} ugrađenih", guide, StringComparison.Ordinal);

        // I da nijedan drugi broj ne stoji uz „ugrađenih" — inače bi obe rečenice mogle da
        // postoje, pa bi test bio zelen nad uputstvom koje protivreči samo sebi.
        foreach (var (value, word) in written)
        {
            if (value == count)
            {
                continue;
            }

            Assert.DoesNotContain($"{word} ugrađenih", guide, StringComparison.Ordinal);
        }
    }
}
