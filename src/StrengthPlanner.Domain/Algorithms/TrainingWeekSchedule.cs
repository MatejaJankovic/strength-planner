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
    /// training day.
    ///
    /// A week shape that is not listed falls back to consecutive days — a plan that still
    /// schedules beats one that throws. A <paramref name="dayIndex"/> outside the week is
    /// a different matter and throws: there is no day to return for a session the week
    /// does not contain, and falling back would silently place two sessions on the same
    /// date (four training days list offsets 0, 1, 3, 4 — index 4 would also land on 4).
    /// </summary>
    public static int OffsetFor(int daysPerWeek, int dayIndex)
    {
        if (daysPerWeek < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(daysPerWeek), daysPerWeek, "Training days per week cannot be negative.");
        }

        if (dayIndex < 0 || dayIndex >= daysPerWeek)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dayIndex), dayIndex, "Day index must fall inside the training week.");
        }

        if (daysPerWeek >= Offsets.Length)
        {
            return dayIndex;
        }

        return Offsets[daysPerWeek][dayIndex];
    }

    /// <summary>
    /// The same, for a template that carries its own week shape.
    ///
    /// The shapes above are chosen by the number of days alone, and for most templates that
    /// is enough: an upper/lower split on Monday, Tuesday, Thursday and Friday never puts the
    /// same movements on two days in a row. A full-body template does - every one of its days
    /// opens with a squat or a hinge - and the handbook asks for at least a day between
    /// "mrtvo dizanje i čučanj". Such a template names its own days; an invalid list is
    /// ignored in favour of the default rather than scheduling two sessions on one date.
    /// </summary>
    public static int OffsetFor(int daysPerWeek, int dayIndex, IReadOnlyList<int>? templateOffsets)
    {
        // Podrazumevani raspored proverava i indeks dana, pa se računa uvek.
        var defaultOffset = OffsetFor(daysPerWeek, dayIndex);

        return templateOffsets is not null && IsValidWeek(templateOffsets, daysPerWeek)
            ? templateOffsets[dayIndex]
            : defaultOffset;
    }

    /// <summary>
    /// A week shape a template may carry: one offset per training day, strictly ascending,
    /// inside a single week.
    /// </summary>
    public static bool IsValidWeek(IReadOnlyList<int> offsets, int daysPerWeek)
    {
        ArgumentNullException.ThrowIfNull(offsets);

        if (offsets.Count != daysPerWeek || daysPerWeek == 0)
        {
            return false;
        }

        for (var index = 0; index < offsets.Count; index++)
        {
            if (offsets[index] < 0 || offsets[index] >= MaxDaysPerWeek
                || (index > 0 && offsets[index] <= offsets[index - 1]))
            {
                return false;
            }
        }

        return true;
    }
}
