using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Enums;

namespace StrengthPlanner.Tests;

/// <summary>
/// „Prati moj šablon" postoji zbog ličnog šablona: tamo je otkucan broj serija namera. Kod
/// ugrađenog šablona propis nije pravljen da poštuje MRV, pa ga balansiranje mora držati u
/// zoni, ma šta zahtev tražio.
/// </summary>
public class SetAllocationRuleTests
{
    [Theory]
    [InlineData(SetAllocation.TargetVolume)]
    [InlineData(SetAllocation.FollowTemplate)]
    public void ACustomTemplate_KeepsWhatWasAsked(SetAllocation requested)
    {
        Assert.Equal(requested, SetAllocationRule.For(requested, isCustomTemplate: true));
    }

    [Theory]
    [InlineData(SetAllocation.TargetVolume)]
    [InlineData(SetAllocation.FollowTemplate)]
    public void ABuiltInTemplate_IsAlwaysBalanced(SetAllocation requested)
    {
        Assert.Equal(SetAllocation.TargetVolume, SetAllocationRule.For(requested, isCustomTemplate: false));
    }
}
