namespace StrengthPlanner.Domain.Algorithms;

/// <summary>
/// Spreads a template's sessions across the seven days of a week.
///
/// The handbook treats rest as part of the plan, not as leftover time: recovery is where
/// adaptation happens, so two hard sessions should not sit back to back if the week has
/// room to separate them. The offsets below place rest days where the week can afford
/// them, and stop pretending to at six sessions — there the lifter trains six days and
/// rests one, which is the point of that split.
/// </summary>
public static class TrainingWeekSchedule
{
    // Dan u nedelji (0 = prvi trenažni dan) za svaki trening, po broju trenažnih dana.
    // Dva dana: ponedeljak/četvrtak — tri dana odmora između, jer full body pogađa sve.
    // Tri dana: klasičan pon/sre/pet.
    // Četiri: dva para po dva dana, sa pauzom u sredini nedelje.
    // Pet: tri pa dva, jedan slobodan dan usred nedelje i vikend na kraju.
    // Šest: šest uzastopnih, sedmi dan slobodan — jedini raspored koji staje.
    private static readonly int[][] Offsets =
    [
        [],
        [0],
        [0, 3],
        [0, 2, 4],
        [0, 1, 3, 4],
        [0, 1, 2, 4, 5],
        [0, 1, 2, 3, 4, 5],
        [0, 1, 2, 3, 4, 5, 6]
    ];

    /// <summary>Longest week the offsets describe.</summary>
    public const int MaxDaysPerWeek = 7;

    /// <summary>
    /// Which day of the week the given session falls on, counting from the week's first
    /// training day. Falls back to consecutive days for any week shape not listed —
    /// a plan that still schedules beats one that throws.
    /// </summary>
    public static int OffsetFor(int daysPerWeek, int dayIndex)
    {
        if (dayIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dayIndex), dayIndex, "Day index cannot be negative.");
        }

        if (daysPerWeek < 0 || daysPerWeek >= Offsets.Length || dayIndex >= Offsets[daysPerWeek].Length)
        {
            return dayIndex;
        }

        return Offsets[daysPerWeek][dayIndex];
    }
}
