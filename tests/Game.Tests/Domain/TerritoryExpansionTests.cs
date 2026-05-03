using Game.Domain;

namespace Game.Tests.Domain;

public sealed class TerritoryExpansionTests
{
    [Fact]
    public void AllowsOwnedSourceToCaptureConnectedNeutralTarget()
    {
        var result = TerritoryExpansion.ValidateNeutralCapture(new NeutralCaptureRequest(
            ActingFactionId: "human-1",
            SourceTerritoryId: "source",
            SourceOwnerFactionId: "human-1",
            TargetTerritoryId: "target",
            TargetOwnerFactionId: null,
            AvailableArmyStrength: 100,
            RequestedStrength: 40,
            HasAllowedRoute: true));

        Assert.True(result.IsValid);
        Assert.Null(result.Error);
    }

    [Fact]
    public void RejectsNonNeighboringTarget()
    {
        var result = TerritoryExpansion.ValidateNeutralCapture(new NeutralCaptureRequest(
            "human-1", "source", "human-1", "target", null, 100, 40, HasAllowedRoute: false));

        Assert.False(result.IsValid);
        Assert.Equal("Target territory is not connected to the source.", result.Error);
    }

    [Fact]
    public void RejectsEnemyTargetForNeutralCaptureSlice()
    {
        var result = TerritoryExpansion.ValidateNeutralCapture(new NeutralCaptureRequest(
            "human-1", "source", "human-1", "target", "npc-1", 100, 40, HasAllowedRoute: true));

        Assert.False(result.IsValid);
        Assert.Equal("Target territory is not neutral in this first slice.", result.Error);
    }

    [Fact]
    public void RejectsStrengthAboveAvailableArmy()
    {
        var result = TerritoryExpansion.ValidateNeutralCapture(new NeutralCaptureRequest(
            "human-1", "source", "human-1", "target", null, 30, 40, HasAllowedRoute: true));

        Assert.False(result.IsValid);
        Assert.Equal("Strength cannot exceed the available source army strength.", result.Error);
    }

    [Fact]
    public void AppliesNeutralCaptureArmyResult()
    {
        var result = TerritoryExpansion.ApplyNeutralCapture(new NeutralCaptureArmyState(
            ActingFactionId: "human-1",
            SourceArmyStrength: 100,
            RequestedStrength: 40));

        Assert.Equal("human-1", result.TargetOwnerFactionId);
        Assert.Equal(60, result.SourceArmyStrength);
        Assert.Equal(40, result.TargetArmyStrength);
    }
}
