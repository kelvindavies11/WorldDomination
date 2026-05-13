using Game.Application;
using Game.Domain;

namespace Game.Tests.Application;

public sealed class CardiffMatchServiceTests
{
    [Fact]
    public void CreatesCardiffMatchWithValidDefaults()
    {
        var service = new CardiffMatchService(new GameMapService());

        var match = service.CreateCardiffMatch();

        Assert.Equal("cardiff-match", match.GameId);
        Assert.Equal("Cardiff & Newport", match.MapArea);
        Assert.Equal("Cardiff & Newport", match.Map.Name);
        Assert.Equal(-3.045, match.Map.Center.Longitude);
        Assert.Equal(51.565, match.Map.Center.Latitude);
        Assert.Equal(2, match.Map.CameraBounds.Count);
        Assert.Equal(18, match.Map.BoundaryCoordinates.Count);
        Assert.Equal(match.Map.BoundaryCoordinates[0], match.Map.BoundaryCoordinates[^1]);
        Assert.True(match.Territories.Count >= 76);
        Assert.Contains(match.Territories, territory => territory.Postcode == "CF64 1");
        Assert.Contains(match.Territories, territory => territory.Postcode == "CF64 2");
        Assert.Contains(match.Territories, territory => territory.Postcode == "CF64 3");
        Assert.Contains(match.Territories, territory => territory.Postcode == "CF64 4");
        Assert.Contains(match.Territories, territory => territory.Postcode == "CF5 6");
        Assert.Equal(8, match.Factions.Count);
        Assert.Equal(2, match.Factions.Count(faction => faction.Kind == FactionKind.Human));
        Assert.Equal(6, match.Factions.Count(faction => faction.Kind == FactionKind.Npc));
        Assert.True(match.Game.IsStarted);
        Assert.Equal("Started", match.Game.Status);
        Assert.Equal(8, match.Armies.Count);
        Assert.All(match.Armies, army => Assert.Equal(100, army.Strength));
        Assert.Equal(match.Territories.Count - 8, match.Territories.Count(territory => territory.OwnerFactionId is null));
        Assert.All(match.Territories, territory =>
        {
            Assert.False(string.IsNullOrWhiteSpace(territory.Postcode));
            Assert.True(
                territory.Postcode.StartsWith("CF", StringComparison.OrdinalIgnoreCase) ||
                territory.Postcode.StartsWith("NP", StringComparison.OrdinalIgnoreCase));
            Assert.NotEmpty(territory.BoundaryCoordinates);
            Assert.Equal(territory.BoundaryCoordinates[0], territory.BoundaryCoordinates[^1]);
            Assert.NotEqual(TerritoryFeatureSummary.Empty, territory.Features);
            Assert.InRange(territory.Stats.Economy, 0, 100);
            Assert.InRange(territory.Stats.Defense, 0, 100);
            Assert.InRange(territory.Stats.Mobility, 0, 100);
            Assert.InRange(territory.Stats.StrategicValue, 0, 100);
        });
        Assert.NotEmpty(match.Routes);
        var humanOneStart = match.Territories.Single(territory => territory.OwnerFactionId == "human-1");
        var humanOneNeutralRoutes = match.Routes
            .Where(route => route.SourceTerritoryId == humanOneStart.Id || route.DestinationTerritoryId == humanOneStart.Id)
            .Select(route => route.SourceTerritoryId == humanOneStart.Id ? route.DestinationTerritoryId : route.SourceTerritoryId)
            .Count(targetId => match.Territories.Single(territory => territory.Id == targetId).OwnerFactionId is null);
        Assert.True(humanOneNeutralRoutes >= 2);
        Assert.Equal(8, match.Leaderboard.Count);
        var humanStanding = match.Leaderboard.Single(row => row.FactionId == "human-1");
        Assert.Equal(1, humanStanding.TerritoryCount);
        Assert.Equal(100, humanStanding.ArmyStrength);
        Assert.True(humanStanding.Revenue > 0);
        Assert.True(humanStanding.ArmyGrowth >= humanStanding.Revenue);
    }

    [Fact]
    public void RandomizesStartingPositionsPerMatchCreation()
    {
        var seededRandom = new Random(1234);
        var service = new CardiffMatchService(
            new GameMapService(),
            new CardiffPostcodeTerritoryRepository(),
            new CardiffTerritoryFeatureRepository(),
            seededRandom);

        var firstMatch = service.CreateCardiffMatch("game-a");
        var secondMatch = service.CreateCardiffMatch("game-b");

        var firstStarts = firstMatch.Factions
            .Select(faction => firstMatch.Territories.Single(territory => territory.OwnerFactionId == faction.Id).Index)
            .ToArray();
        var secondStarts = secondMatch.Factions
            .Select(faction => secondMatch.Territories.Single(territory => territory.OwnerFactionId == faction.Id).Index)
            .ToArray();

        Assert.Equal(8, firstStarts.Distinct().Count());
        Assert.Equal(8, secondStarts.Distinct().Count());
        Assert.False(firstStarts.SequenceEqual(secondStarts));
    }

    [Fact]
    public void CreatesLobbyMatchWithNpcStartsAndSelectedHumanStarts()
    {
        var service = new CardiffMatchService(new GameMapService());
        var setup = new MatchSetupOptions(
            GameId: "custom-game",
            MapArea: "Cardiff",
            HumanPlayers: 2,
            MaxHumanPlayers: 4,
            NpcFactions: 3,
            TerritoryCount: 100,
            Status: "Open",
            IsStarted: false,
            HumanStartTerritoriesByFactionId: new Dictionary<string, string>
            {
                ["human-1"] = "postcode-cf64-1"
            },
            HumanPlayerNamesByFactionId: new Dictionary<string, string>());

        var match = service.CreateCardiffLobbyMatch(setup);

        Assert.Equal("custom-game", match.GameId);
        Assert.False(match.Game.IsStarted);
        Assert.Equal("Open", match.Game.Status);
        Assert.Equal(2, match.Game.HumanPlayers);
        Assert.Equal(4, match.Game.MaxHumanPlayers);
        Assert.Equal(5, match.Factions.Count);
        Assert.Equal(2, match.Factions.Count(faction => faction.Kind == FactionKind.Human));
        Assert.Equal(3, match.Factions.Count(faction => faction.Kind == FactionKind.Npc));
        Assert.Equal("human-1", match.Territories.Single(territory => territory.Id == "postcode-cf64-1").OwnerFactionId);
        Assert.DoesNotContain(match.Territories, territory => territory.OwnerFactionId == "human-2");
        Assert.Equal(4, match.Armies.Count);
        Assert.Equal(1, match.Armies.Count(army => army.FactionId == "human-1"));
        Assert.Equal(3, match.Armies.Count(army => army.FactionId.StartsWith("npc-", StringComparison.Ordinal)));
    }
}
