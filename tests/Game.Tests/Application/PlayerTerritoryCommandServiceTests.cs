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
        var standing = result.Snapshot.Leaderboard.Single(row => row.FactionId == "human-1");
        Assert.True(standing.MapControlPercentage > 50);
        Assert.Equal(2, standing.TerritoryCount);
        Assert.Equal(14, standing.Revenue);
        Assert.Equal(100, standing.ArmyStrength);
        Assert.Equal(48, standing.ArmyGrowth);
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
    public void CapturesEnemyTerritoryWhenAttackOvercomesDefenders()
    {
        var state = new CardiffMatchStateService(CreateSnapshot(sourceArmyStrength: 140, enemyArmyStrength: 60, enemyDefense: 20));
        var service = new PlayerTerritoryCommandService(state);

        var result = service.SendArmyToNeutralTerritory(new SendArmyCommand(
            PlayerFactionId: "human-1",
            SourceTerritoryId: "source",
            TargetTerritoryId: "enemy",
            Strength: 80));

        Assert.True(result.Accepted);
        Assert.NotNull(result.Snapshot);
        Assert.Equal("human-1", result.Snapshot.Territories.Single(territory => territory.Id == "enemy").OwnerFactionId);
        Assert.Equal(60, result.Snapshot.Armies.Single(army => army.TerritoryId == "source").Strength);
        Assert.Equal(14, result.Snapshot.Armies.Single(army => army.TerritoryId == "enemy").Strength);
    }

    [Fact]
    public void DefenderKeepsEnemyTerritoryWhenDefenseTurnsTheBattle()
    {
        var state = new CardiffMatchStateService(CreateSnapshot(sourceArmyStrength: 120, enemyArmyStrength: 80, enemyDefense: 100));
        var service = new PlayerTerritoryCommandService(state);

        var result = service.SendArmyToNeutralTerritory(new SendArmyCommand(
            PlayerFactionId: "human-1",
            SourceTerritoryId: "source",
            TargetTerritoryId: "enemy",
            Strength: 100));

        Assert.True(result.Accepted);
        Assert.NotNull(result.Snapshot);
        Assert.Equal("npc-1", result.Snapshot.Territories.Single(territory => territory.Id == "enemy").OwnerFactionId);
        Assert.Equal(20, result.Snapshot.Armies.Single(army => army.TerritoryId == "source").Strength);
        Assert.Equal(13, result.Snapshot.Armies.Single(army => army.TerritoryId == "enemy").Strength);
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

    private static MatchSnapshot CreateSnapshot(int sourceArmyStrength = 100, int enemyArmyStrength = 100, int enemyDefense = 0)
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
            Territory("enemy", "npc-1", 25, enemyDefense),
            Territory("distant", null, 25)
        };
        var armies = new[]
        {
            new MatchArmyDto("army-human-1", "human-1", "source", sourceArmyStrength),
            new MatchArmyDto("army-npc-1", "npc-1", "enemy", enemyArmyStrength)
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
            Game: new MatchGameStateDto("Started", true, 1, 1, 1),
            Map: new MatchMapDto("cardiff", "Cardiff", new MapCoordinateDto(0, 0), [], []),
            Factions: factions,
            Territories: territories,
            Armies: armies,
            Routes: routes,
            Leaderboard: leaderboard);
    }

    private static MatchTerritoryDto Territory(string id, string? ownerFactionId, double areaSquareKm, int defense = 0) =>
        new(
            Id: id,
            Index: 0,
            Name: id,
            AreaSquareKm: areaSquareKm,
            OwnerFactionId: ownerFactionId,
            Stats: id switch
            {
                "source" => new TerritoryStats(10, 0, 5, 20, 0, 0),
                "neutral" => new TerritoryStats(4, 0, 3, 6, 0, 0),
                "enemy" => new TerritoryStats(7, defense, 2, 8, 0, 0),
                _ => new TerritoryStats(0, 0, 0, 0, 0, 0)
            },
            Postcode: "CF",
            Features: TerritoryFeatureSummary.Empty,
            BoundaryCoordinates: []);
}
