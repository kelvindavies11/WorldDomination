using Game.Application;
using Game.Domain;

namespace Game.Tests.Application;

public sealed class CardiffMatchServiceTests
{
    [Fact]
    public void CreatesDeterministicCardiffMatchWithDocumentedDefaults()
    {
        var service = new CardiffMatchService(new GameMapService());

        var match = service.CreateCardiffMatch();

        Assert.Equal("cardiff-match", match.GameId);
        Assert.Equal("Cardiff", match.MapArea);
        Assert.Equal("Cardiff", match.Map.Name);
        Assert.Equal(-3.1791, match.Map.Center.Longitude);
        Assert.Equal(51.4816, match.Map.Center.Latitude);
        Assert.Equal(2, match.Map.CameraBounds.Count);
        Assert.Equal(15, match.Map.BoundaryCoordinates.Count);
        Assert.Equal(match.Map.BoundaryCoordinates[0], match.Map.BoundaryCoordinates[^1]);
        Assert.True(match.Territories.Count >= 58);
        Assert.Contains(match.Territories, territory => territory.Postcode == "CF64 1");
        Assert.Contains(match.Territories, territory => territory.Postcode == "CF64 2");
        Assert.Contains(match.Territories, territory => territory.Postcode == "CF64 3");
        Assert.Contains(match.Territories, territory => territory.Postcode == "CF64 4");
        Assert.Contains(match.Territories, territory => territory.Postcode == "CF5 6");
        Assert.Equal(8, match.Factions.Count);
        Assert.Equal(2, match.Factions.Count(faction => faction.Kind == FactionKind.Human));
        Assert.Equal(6, match.Factions.Count(faction => faction.Kind == FactionKind.Npc));
        Assert.Equal(8, match.Armies.Count);
        Assert.All(match.Armies, army => Assert.Equal(100, army.Strength));
        Assert.Equal(match.Territories.Count - 8, match.Territories.Count(territory => territory.OwnerFactionId is null));
        Assert.All(match.Territories, territory =>
        {
            Assert.False(string.IsNullOrWhiteSpace(territory.Postcode));
            Assert.StartsWith("CF", territory.Postcode);
            Assert.NotEmpty(territory.BoundaryCoordinates);
            Assert.Equal(territory.BoundaryCoordinates[0], territory.BoundaryCoordinates[^1]);
            Assert.NotEqual(TerritoryFeatureSummary.Empty, territory.Features);
            Assert.InRange(territory.Stats.Economy, 0, 100);
            Assert.InRange(territory.Stats.Defense, 0, 100);
            Assert.InRange(territory.Stats.Mobility, 0, 100);
            Assert.InRange(territory.Stats.StrategicValue, 0, 100);
        });
        Assert.NotEmpty(match.Routes);
        Assert.Equal(8, match.Leaderboard.Count);
    }

    [Fact]
    public void SpreadsNpcStartsAcrossTheTerritoryList()
    {
        var service = new CardiffMatchService(new GameMapService());

        var match = service.CreateCardiffMatch();
        var npcStartIndexes = match.Factions
            .Where(faction => faction.Kind == FactionKind.Npc)
            .Select(faction => match.Territories.Single(territory => territory.OwnerFactionId == faction.Id).Index)
            .Order()
            .ToArray();

        Assert.Equal(6, npcStartIndexes.Length);
        Assert.True(npcStartIndexes.SequenceEqual(npcStartIndexes.Order()));
        Assert.All(npcStartIndexes, index => Assert.InRange(index, 1, match.Territories.Count - 2));
    }
}
