using StrengthPlanner.Domain.Algorithms;

namespace StrengthPlanner.Tests;

/// <summary>
/// A workout corrected after its week was counted must take that week's move of the landmarks
/// back before the week is counted again. The landmarks store only their current values, so
/// the move is recorded per week and undone with <see cref="VolumeAdaptation.Undo"/>.
/// </summary>
public class VolumeAdaptationUndoTests
{
    private static readonly VolumeLandmarkValues Seed = new(Mev: 10, Mav: 16, Mrv: 22);

    /// <summary>Weeks that move something, something else, and nothing.</summary>
    public static IEnumerable<object[]> Responses()
    {
        foreach (var sets in new[] { 4m, 10m, 16m, 20m, 24m })
        foreach (var deviation in new[] { -1.5m, 0m, 1.5m })
        foreach (var failures in new[] { 0m, 0.1m, 0.4m })
        foreach (var strength in new decimal?[] { null, -0.05m, 0m, 0.05m })
        {
            yield return
            [
                new VolumeResponse(
                    PerformedSets: sets,
                    RawSets: sets,
                    AverageRirDeviation: deviation,
                    FailureShare: failures,
                    StrengthChangeShare: strength,
                    PreviousStrengthChangeShare: strength)
            ];
        }
    }

    /// <summary>
    /// The week that was counted last is taken back exactly: the landmarks are what they were
    /// before it, for every kind of week and for landmarks that already moved before.
    /// </summary>
    [Theory]
    [MemberData(nameof(Responses))]
    public void UndoingTheLastWeek_RestoresTheLandmarksItStartedFrom(VolumeResponse response)
    {
        foreach (var before in StartingPoints())
        {
            var after = VolumeAdaptation.Adjust(before, Seed, response);
            var shift = LandmarkShift.Between(before, after);

            Assert.Equal(before, VolumeAdaptation.Undo(after, Seed, shift));
        }
    }

    /// <summary>
    /// With later weeks counted on top, the result is not the original but is still a band the
    /// rules allow: inside the drift around the seed, wide enough, the target inside it.
    /// </summary>
    [Theory]
    [MemberData(nameof(Responses))]
    public void UndoingAnEarlierWeek_StillGivesAValidBand(VolumeResponse response)
    {
        var first = VolumeAdaptation.Adjust(Seed, Seed, response);
        var firstShift = LandmarkShift.Between(Seed, first);
        var later = VolumeAdaptation.Adjust(first, Seed, response);

        var undone = VolumeAdaptation.Undo(later, Seed, firstShift);

        Assert.True(undone.Mrv - undone.Mev >= VolumeAdaptation.MinBandWidth);
        Assert.InRange(undone.Mav, undone.Mev + 1, undone.Mrv - 1);
        Assert.InRange(undone.Mev, Seed.Mev * (1 - VolumeAdaptation.MaxDriftFromSeed), Seed.Mev * (1 + VolumeAdaptation.MaxDriftFromSeed));
        Assert.InRange(undone.Mrv, Seed.Mrv * (1 - VolumeAdaptation.MaxDriftFromSeed), Seed.Mrv * (1 + VolumeAdaptation.MaxDriftFromSeed));
    }

    /// <summary>A week that moved nothing has nothing to take back.</summary>
    [Fact]
    public void AZeroShift_ChangesNothing()
    {
        var shift = LandmarkShift.Between(Seed, Seed);

        Assert.True(shift.IsZero);
        Assert.Equal(Seed, VolumeAdaptation.Undo(Seed, Seed, shift));
    }

    /// <summary>
    /// The move is the difference, field by field, and survives the clamps that held part of
    /// an intended move back: only what actually happened is recorded.
    /// </summary>
    [Fact]
    public void TheShift_IsWhatActuallyHappened_NotWhatWasIntended()
    {
        // The seed's MRV is 22, so 33 is the ceiling of the allowed drift: a week that wants
        // to raise it again changes nothing.
        var atTheCeiling = new VolumeLandmarkValues(Seed.Mev, Seed.Mav, Mrv: 33);
        var wantsMore = new VolumeResponse(PerformedSets: 32m, RawSets: 32m, AverageRirDeviation: 1.5m, FailureShare: 0m);

        var after = VolumeAdaptation.Adjust(atTheCeiling, Seed, wantsMore);

        Assert.True(LandmarkShift.Between(atTheCeiling, after).IsZero);
    }

    private static IEnumerable<VolumeLandmarkValues> StartingPoints()
    {
        yield return Seed;
        yield return new VolumeLandmarkValues(11, 17, 23);
        yield return new VolumeLandmarkValues(9, 15, 21);
        yield return new VolumeLandmarkValues(12, 15, 18);
    }
}
