using Game.Application;
using Game.Domain;

namespace Game.Tests.Application;

public sealed class PlayerTerritoryCommandServiceTests
{
    [Fact]
    public void CapturesConnectedNeutralTerritoryAndUpdatesArmiesAndLeaderboard()
    {
        var state = new CardiffMatchStateService(CreateSnapshot());
        var service = new PlayerTerritoryCommandService(state);

        var result = service.SendArmyToNeutralTerritory(new SendArmyCommand(
            PlayerFactionId: "human-1",
            SourceTerritoryId: "source",
            TargetTerritoryId: "neutral",
            Strength: 40));

        Assert.True(result.Accepted);
        Assert.Null(result.Error);
        Assert.NotNull(result.Snapshot);
        Assert.Equal("human-1", result.Snapshot.Territories.Single(territory => territory.Id == "neutral").OwnerFactionId);
        Assert.Equal(60, result.Snapshot.Armies.Single(army => army.TerritoryId == "source").Strength);
        Assert.Equal(40, result.Snapshot.Armies.Single(army => army.TerritoryId == "neutral").Strength);
        Assert.True(result.EtaSeconds > 0);
        Assert.True(result.Snapshot.Leaderboard.Single(row => row.FactionId == "human-1").MapControlPercentage > 50);
    }

    [Fact]
    public void RejectsNonConnectedTarget()
    {
        var state = new CardiffMatchStateService(CreateSnapshot());
        var service = new PlayerTerritoryCommandService(state);

        var result = service.SendArmyToNeutralTerritory(new SendArmyCommand(
            PlayerFactionId: "human-1",
            SourceTerritoryId: "source",
            TargetTerritoryId: "distant",
            Strength: 40));

        Assert.False(result.Accepted);
        Assert.Equal("Target territory is not connected to the source.", result.Error);
    }

    [Fact]
    public void RejectsEnemyTargetForThisSlice()
    {
        var state = new CardiffMatchStateService(CreateSnapshot());
        var service = new PlayerTerritoryCommandService(state);

        var result = service.SendArmyToNeutralTerritory(new SendArmyCommand(
            PlayerFactionId: "human-1",
            SourceTerritoryId: "source",
            TargetTerritoryId: "enemy",
            Strength: 40));

        Assert.False(result.Accepted);
        Assert.Equal("Target territory is not neutral in this first slice.", result.Error);
    }

    [Fact]
    public void RejectsStrengthAboveSourceArmy()
    {
        var state = new CardiffMatchStateService(CreateSnapshot());
        var service = new PlayerTerritoryCommandService(state);

        var result = service.SendArmyToNeutralTerritory(new SendArmyCommand(
            PlayerFactionId: "human-1",
            SourceTerritoryId: "source",
            TargetTerritoryId: "neutral",
            Strength: 140));

        Assert.False(result.Accepted);
        Assert.Equal("Strength cannot exceed the available source army strength.", result.Error);
    }

    private static MatchSnapshot CreateSnapshot()
    {
        var factions = new[]
        {
            new MatchFactionDto("human-1", "Player 1", FactionKind.Human, "#1f8a70"),
            new MatchFactionDto("npc-1", "NPC 1", FactionKind.Npc, "#c58a1a")
        };
        var territories = new[]
        {
            Territory("source", "human-1", 50),
            Territory("neutral", null, 25),
            Territory("enemy", "npc-1", 25),
            Territory("distant", null, 25)
        };
        var armies = new[]
        {
            new MatchArmyDto("army-human-1", "human-1", "source", 100),
            new MatchArmyDto("army-npc-1", "npc-1", "enemy", 100)
        };
        var routes = new[]
        {
            new MatchRouteDto("source", "neutral", RouteTransport.Road, 70, IsAllowed: true),
            new MatchRouteDto("source", "enemy", RouteTransport.Road, 80, IsAllowed: true)
        };
        var leaderboard = MapControlCalculator.CalculateLeaderboard(
            territories.Select(territory => new ControlledTerritory(territory.Id, territory.OwnerFactionId, territory.AreaSquareKm)).ToArray(),
            factions.Select(faction => new FactionStanding(faction.Id, faction.Name, 0)).ToArray());

        return new MatchSnapshot(
            GameId: "cardiff-match",
            MapArea: "Cardiff",
            SnapshotGeneratedAtUtc: DateTimeOffset.UtcNow,
            Map: new MatchMapDto("cardiff", "Cardiff", new MapCoordinateDto(0, 0), [], []),
            Factions: factions,
            Territories: territories,
            Armies: armies,
            Routes: routes,
            Leaderboard: leaderboard);
    }

    private static MatchTerritoryDto Territory(string id, string? ownerFactionId, double areaSquareKm) =>
        new(
            Id: id,
            Index: 0,
            Name: id,
            AreaSquareKm: areaSquareKm,
            OwnerFactionId: ownerFactionId,
            Stats: new TerritoryStats(0, 0, 0, 0),
            Postcode: "CF",
            Features: TerritoryFeatureSummary.Empty,
            BoundaryCoordinates: []);
}
