namespace StrengthPlanner.Domain.Algorithms;

/// <summary>
/// Weekly working-set limits for one muscle group.
///
/// The handbook names three, not two: below MEV nothing happens, above MRV recovery
/// fails, and MAV is the volume actually worth aiming for in between —
/// <i>"Optimalan volumen treninga pri kojem se telo adekvatno oporavlja i stimuliše
/// hipertrofiju, bez ulaska u prekomeran zamor"</i>. Without MAV the optimal band for
/// chest runs from 10 to 22 sets, which tells the lifter where they are not, but never
/// where to aim.
/// </summary>
/// <param name="Mev">Minimum effective volume — below this the week is unlikely to drive growth.</param>
/// <param name="Mav">Maximum adaptive volume — the target the plan should sit at.</param>
/// <param name="Mrv">Maximum recoverable volume — above this the week outruns recovery.</param>
public sealed record VolumeLandmarkValues(int Mev, int Mav, int Mrv);
