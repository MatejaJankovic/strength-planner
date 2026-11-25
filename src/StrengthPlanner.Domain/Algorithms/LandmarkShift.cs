namespace StrengthPlanner.Domain.Algorithms;

/// <summary>
/// How far one completed week moved a muscle's volume landmarks: the difference between what
/// they were before the week was counted and what they were after.
///
/// Stored per week so a workout corrected after its week was counted can take that week's move
/// back before the week is counted again (see <see cref="VolumeAdaptation.Undo"/>).
/// </summary>
public sealed record LandmarkShift(int Mev, int Mav, int Mrv)
{
    /// <summary>The move from <paramref name="before"/> to <paramref name="after"/>.</summary>
    public static LandmarkShift Between(VolumeLandmarkValues before, VolumeLandmarkValues after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        return new LandmarkShift(after.Mev - before.Mev, after.Mav - before.Mav, after.Mrv - before.Mrv);
    }

    /// <summary>True when the week left the landmarks where they were.</summary>
    public bool IsZero => Mev == 0 && Mav == 0 && Mrv == 0;
}
