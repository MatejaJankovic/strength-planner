using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Tests;

/// <summary>
/// Deload prepolovi serije i spusti opterećenje na 90% stvarno korišćenog, ali je ciljni
/// RIR ostajao onaj cilja bloka — a to dvoje ne ide zajedno.
///
/// Sa maksimumom od 130 kg deload pada na 90 kg, i RIR 1 se na toj težini dostiže tek oko
/// dvanaestog ponavljanja. Po naporu je to skoro normalna radna serija, dakle tačno ono što
/// rasterećenje nije. Deset odsto maksimuma po Epley-u vredi oko tri efektivna ponavljanja,
/// pa se isti opseg na 90% odrađuje sa približno tri više u rezervi.
/// </summary>
public class DeloadIntensityTests
{
    private const int Sets = 4;

    [Theory]
    [InlineData(Goal.Hypertrophy, 1, 3)]
    [InlineData(Goal.Strength, 2, 4)]
    public void DeloadWeek_LeavesMoreInReserveThanTheGoalAsksFor(
        Goal goal,
        int expectedGoalRir,
        int expectedDeloadRir)
    {
        var prescription = GoalPrescriptions.ForGoal(goal);

        Assert.Equal(expectedGoalRir, prescription.TargetRir);
        Assert.Equal(expectedDeloadRir, Periodization.DeloadRir(prescription.TargetRir));
    }

    [Theory]
    [InlineData(PeriodizationModel.Flat)]
    [InlineData(PeriodizationModel.Linear)]
    [InlineData(PeriodizationModel.Inverse)]
    public void EveryModel_DeloadsAtTheRaisedRir(PeriodizationModel model)
    {
        var goal = GoalPrescriptions.ForGoal(Goal.Hypertrophy);
        var weeks = Periodization.ForBlock(
            model,
            goal.RepRangeMin,
            goal.RepRangeMax,
            goal.TargetRir,
            Sets);

        var deload = weeks[^1];

        Assert.True(deload.IsDeload);
        Assert.Equal(Periodization.DeloadRir(goal.TargetRir), deload.TargetRir);

        // Opseg se ne dira: menjanje opsega bi promenilo i sam pokret, a ne samo napor.
        Assert.Equal(goal.RepRangeMin, deload.RepRangeMin);
        Assert.Equal(goal.RepRangeMax, deload.RepRangeMax);
    }

    /// <summary>
    /// Pomeraj ne sme da izađe iz pojasa u kome RIR uopšte nosi značenje: iznad
    /// <see cref="Periodization.MaxRir"/> serija prestaje da pokreće adaptaciju, a ispod
    /// <see cref="Periodization.MinRir"/> umor nema manjak koji bi merio.
    /// </summary>
    [Fact]
    public void TheRaisedRir_StaysInsideTheBandThatMeansSomething()
    {
        for (var baseRir = Periodization.MinRir; baseRir <= Periodization.MaxRir; baseRir++)
        {
            var deloadRir = Periodization.DeloadRir(baseRir);

            Assert.InRange(deloadRir, Periodization.MinRir, Periodization.MaxRir);
            Assert.True(deloadRir >= baseRir, $"osnova {baseRir} -> deload {deloadRir}");
        }
    }

    /// <summary>
    /// Hipertrofijski deload staje tačno na granici punog kredita u volumenu
    /// (<see cref="StimulativeVolume.FullCreditRir"/>), pa se njegove serije još broje kao
    /// volumen. Kod snage pomeraj svesno prelazi u pola kredita: serije su prepolovljene i
    /// lakše, i nedelja ne treba da izgleda kao pun stimulus.
    /// </summary>
    [Fact]
    public void WhatTheRaisedRirMeansForTheVolumeCount()
    {
        var hypertrophy = Periodization.DeloadRir(GoalPrescriptions.ForGoal(Goal.Hypertrophy).TargetRir);
        var strength = Periodization.DeloadRir(GoalPrescriptions.ForGoal(Goal.Strength).TargetRir);

        Assert.Equal(1m, StimulativeVolume.CreditFor(hypertrophy, isFailure: false));
        Assert.Equal(StimulativeVolume.PartialCredit, StimulativeVolume.CreditFor(strength, isFailure: false));
    }

    /// <summary>
    /// Nedelja koja je bila planirani deload pa je oslobođena mora da se vrati na RIR
    /// <b>cilja</b>, a ne na deload RIR. Pomeraj se ne obrće iz zapisanog plana — osnova
    /// dolazi iz cilja bloka, jer se odsecanje ne može obrnuti iz svog rezultata.
    /// </summary>
    [Fact]
    public void ARestoredWeek_TakesTheGoalRir_NotTheDeloadRir()
    {
        var goal = GoalPrescriptions.ForGoal(Goal.Hypertrophy);
        var deloadRir = Periodization.DeloadRir(goal.TargetRir);

        var fromGoal = Periodization.ForWeek(
            PeriodizationModel.Linear,
            2,
            goal.RepRangeMin,
            goal.RepRangeMax,
            goal.TargetRir,
            Sets);

        var fromDeloadRir = Periodization.ForWeek(
            PeriodizationModel.Linear,
            2,
            goal.RepRangeMin,
            goal.RepRangeMax,
            deloadRir,
            Sets);

        Assert.NotEqual(fromGoal.TargetRir, fromDeloadRir.TargetRir);
        Assert.Equal(goal.TargetRir, fromGoal.TargetRir);
    }
}
