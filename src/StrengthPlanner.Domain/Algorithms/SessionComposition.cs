using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Domain.Algorithms;

/// <summary>
/// Picks which of a template day's exercises a given lifter actually does.
///
/// Templates are written compounds-first, deepest movement first, which is the order the
/// handbook prescribes anyway (<i>"Složene vežbe radiš pre izolacionih vežbi, jer su
/// zamornije"</i>). That ordering is what lets one rule serve every template: take the
/// compounds the level allows from the front, then fill the remaining slots with
/// isolations.
///
/// The shape of the result differs sharply by level, which is the handbook's point — a
/// beginner's session is two or three big lifts and little else, while an advanced
/// lifter's is one heavy movement surrounded by targeted isolation work.
/// </summary>
public static class SessionComposition
{
    /// <summary>
    /// Fewest exercises a session may hold. A session trimmed below this stops being a
    /// session, so this wins over the compound budget on days that offer nothing else.
    /// </summary>
    public const int MinExercisesPerSession = 3;

    /// <summary>
    /// Returns the exercises to program for <paramref name="level"/>, preserving the
    /// template's order so compounds still come first.
    /// </summary>
    public static IReadOnlyList<T> ForLevel<T>(
        IReadOnlyList<T> ordered,
        Func<T, bool> isCompound,
        ExperienceLevel level)
    {
        ArgumentNullException.ThrowIfNull(ordered);
        ArgumentNullException.ThrowIfNull(isCompound);

        if (ordered.Count == 0)
        {
            return ordered;
        }

        var compoundBudget = ExperienceProgramming.MaxCompoundsPerSession(level);
        var isolationCount = ordered.Count(exercise => !isCompound(exercise));

        // Trening se ne popunjava do maksimuma po svaku cenu: ako dan nema dovoljno
        // izolacija, naprednom vežbaču je ispravnije dati kraći trening nego mu nabiti
        // složene vežbe preko dozvoljenog broja.
        var wanted = compoundBudget + isolationCount;
        var ceiling = Math.Min(ExperienceProgramming.ExercisesPerSession(level), ordered.Count);

        // Prag se poravnava na plafon umesto da se pretpostavi da je ispod njega —
        // Math.Clamp baca izuzetak kada je donja granica iznad gornje, a to bi zavisilo
        // od odnosa dve konstante koje neko kasnije može da promeni.
        var floor = Math.Min(Math.Min(MinExercisesPerSession, ordered.Count), ceiling);
        var slots = Math.Clamp(wanted, floor, ceiling);

        // Bira se po indeksu, ne po vrednosti: šablon sme da ponovi istu vežbu u danu,
        // a poređenje po jednakosti bi tada uzelo obe pojave i probilo broj mesta.
        var chosenIndices = new HashSet<int>();
        var compoundsTaken = 0;

        // Prvi prolaz: složene vežbe do dozvoljenog broja, pa izolacije.
        for (var index = 0; index < ordered.Count && chosenIndices.Count < slots; index++)
        {
            if (isCompound(ordered[index]))
            {
                if (compoundsTaken < compoundBudget)
                {
                    chosenIndices.Add(index);
                    compoundsTaken++;
                }

                continue;
            }

            chosenIndices.Add(index);
        }

        // Dan sastavljen samo od složenih vežbi (npr. čučanj, mrtvo, leg press) ne može
        // da ispuni i donji prag i budžet istovremeno. Tada pobeđuje prag: trening od
        // jedne vežbe nije trening. Ovo je jedini slučaj u kome se budžet prekoračuje.
        for (var index = 0; index < ordered.Count && chosenIndices.Count < slots; index++)
        {
            chosenIndices.Add(index);
        }

        // Redosled iz šablona je trenažno pravilo, a ne slučajnost — složeno pre izolacije.
        return chosenIndices.OrderBy(index => index).Select(index => ordered[index]).ToList();
    }
}
