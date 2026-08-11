namespace StrengthPlanner.Domain.Algorithms;

/// <summary>
/// A pair of weekly working-set limits for one muscle group.
/// </summary>
/// <param name="Mev">Minimum effective volume — below this the week is unlikely to drive growth.</param>
/// <param name="Mrv">Maximum recoverable volume — above this the week outruns recovery.</param>
public sealed record VolumeLandmarkValues(int Mev, int Mrv);
