using Game.Application;
using Game.Domain;

namespace Game.Tests.Application;

public sealed class ArmyReinforcementCalculatorTests
{
    // -----------------------------------------------------------------------
    // Calculate — grant generation
    // -----------------------------------------------------------------------

    [Fact]
    public void ReturnsGrantForEachOwnedTerritoryWithArmy()
    {
        var snapshot = CreateSnapshot();

        var grants = ArmyReinforcementCalculator.Calculate(snapshot);

        Assert.Equal(2, grants.Count);
        Assert.Contains(grants, g => g.FactionId == "human-1" && g.TerritoryId == "human-source");
        Assert.Contains(grants, g => g.FactionId == "npc-1" && g.TerritoryId == "npc-source");
    }

    [Fact]
    public void ReturnsNoGrantsWhenGameIsNotStarted()
    {
        var snapshot = CreateSnapshot() with
        {
            Game = new MatchGameStateDto("Open", false, 1, 2, 1)
        };

        var grants = ArmyReinforcementCalculator.Calculate(snapshot);

        Assert.Empty(grants);
    }

    [Fact]
    public void ReturnsNoGrantForNeutralTerritory()
    {
        var snapshot = CreateSnapshot();

        var grants = ArmyReinforcementCalculator.Calculate(snapshot);

        Assert.DoesNotContain(grants, g => g.TerritoryId == "neutral");
    }

    [Fact]
    public void ReturnsNoGrantForOwnedTerritoryWithoutArmy()
    {
        var snapshot = CreateSnapshot() with
        {
            Armies = []
        };

        var grants = ArmyReinforcementCalculator.Calculate(snapshot);

        Assert.Empty(grants);
    }

    [Fact]
    public void GrantAmountIsAtLeastOne()
    {
        // Economy = 0 → Math.Max(1, 0/10) = 1
        var snapshot = CreateSnapshot(economy: 0);

        var grants = ArmyReinforcementCalculator.Calculate(snapshot);

        Assert.All(grants, g => Assert.Equal(1, g.Amount));
    }

    [Fact]
    public void GrantAmountScalesWithEconomy()
    {
        // Economy = 50 → Math.Max(1, 50/10) = 5
        var snapshot = CreateSnapshot(economy: 50);

        var grants = ArmyReinforcementCalculator.Calculate(snapshot);

        Assert.All(grants, g => Assert.Equal(5, g.Amount));
    }

    [Fact]
    public void GrantAmountCapsAtEconomyDividedByTen()
    {
        // Economy = 100 → Math.Max(1, 100/10) = 10
        var snapshot = CreateSnapshot(economy: 100);

        var grants = ArmyReinforcementCalculator.Calculate(snapshot);

        Assert.All(grants, g => Assert.Equal(10, g.Amount));
    }

    // -----------------------------------------------------------------------
    // Apply — snapshot mutation
    // -----------------------------------------------------------------------

    [Fact]
    public void ApplyIncreasesArmyStrengthByGrantAmount()
    {
        var snapshot = CreateSnapshot(economy: 50); // grant = 5 per territory
        var grants = ArmyReinforcementCalculator.Calculate(snapshot);

        var updated = ArmyReinforcementCalculator.Apply(snapshot, grants);

        var humanArmy = updated.Armies.Single(a => a.FactionId == "human-1" && a.TerritoryId == "human-source");
        Assert.Equal(105, humanArmy.Strength); // 100 + 5
    }

    [Fact]
    public void ApplyDoesNotChangeNeutralTerritories()
    {
        var snapshot = CreateSnapshot(economy: 50);
        var grants = ArmyReinforcementCalculator.Calculate(snapshot);

        var updated = ArmyReinforcementCalculator.Apply(snapshot, grants);

        // Neutral territory has no army in either snapshot
        Assert.DoesNotContain(updated.Armies, a => a.TerritoryId == "neutral");
    }

    [Fact]
    public void ApplyReturnsUnchangedSnapshotWhenNoGrants()
    {
        var snapshot = CreateSnapshot();

        var updated = ArmyReinforcementCalculator.Apply(snapshot, []);

        Assert.Same(snapshot, updated);
    }

    [Fact]
    public void ApplyDoesNotMutateOriginalSnapshot()
    {
        var snapshot = CreateSnapshot(economy: 50);
        var grants = ArmyReinforcementCalculator.Calculate(snapshot);
        var originalStrength = snapshot.Armies.Single(a => a.FactionId == "human-1").Strength;

        ArmyReinforcementCalculator.Apply(snapshot, grants);

        Assert.Equal(originalStrength, snapshot.Armies.Single(a => a.FactionId == "human-1").Strength);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static MatchSnapshot CreateSnapshot(int economy = 10)
    {
        var stats = new TerritoryStats(Economy: economy, Defense: 0, Mobility: 5, StrategicValue: 2, RevenuePerTick: 0, ArmyGrowthPerTick: 0);
        var factions = new[]
        {
            new MatchFactionDto("human-1", "Player 1", FactionKind.Human, "#1f8a70"),
            new MatchFactionDto("npc-1", "NPC 1", FactionKind.Npc, "#c58a1a")
        };
        var territories = new[]
        {
            Territory("human-source", "human-1", stats),
            Territory("npc-source", "npc-1", stats),
            Territory("neutral", null, stats)
        };
        var armies = new[]
        {
            new MatchArmyDto("army-human-1", "human-1", "human-source", 100),
            new MatchArmyDto("army-npc-1", "npc-1", "npc-source", 100)
        };
        var leaderboard = MapControlCalculator.CalculateLeaderboard(
            territories.Select(t => new ControlledTerritory(t.Id, t.OwnerFactionId, t.AreaSquareKm, t.Stats)).ToArray(),
            factions.Select(f => new FactionStanding(f.Id, f.Name, 0)).ToArray());

        return new MatchSnapshot(
            GameId: "test-game",
            MapArea: "Cardiff",
            SnapshotGeneratedAtUtc: DateTimeOffset.UtcNow,
            Game: new MatchGameStateDto("Started", true, 1, 1, 1),
            Map: new MatchMapDto("cardiff", "Cardiff", new MapCoordinateDto(0, 0), [], []),
            Factions: factions,
            Territories: territories,
            Armies: armies,
            Routes: [],
            Leaderboard: leaderboard);
    }

    private static MatchTerritoryDto Territory(string id, string? ownerFactionId, TerritoryStats stats) =>
        new(
            Id: id,
            Index: 0,
            Name: id,
            AreaSquareKm: 2.5,
            OwnerFactionId: ownerFactionId,
            Stats: stats,
            Postcode: null,
            Features: TerritoryFeatureSummary.Empty,
            BoundaryCoordinates: []);
}
