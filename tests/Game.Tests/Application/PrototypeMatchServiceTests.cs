using Game.Application;

namespace Game.Tests.Application;

public sealed class PrototypeMatchServiceTests
{
    [Fact]
    public void CreatesDeterministicCardiffPrototypeWithDocumentedDefaults()
    {
        var service = new PrototypeMatchService();

        var match = service.CreateCardiffPrototype();

        Assert.Equal("cardiff-prototype", match.GameId);
        Assert.Equal("Cardiff", match.MapArea);
        Assert.Equal(100, match.Territories.Count);
        Assert.Equal(8, match.Factions.Count);
        Assert.Equal(2, match.Factions.Count(faction => faction.Kind == FactionKind.Human));
        Assert.Equal(6, match.Factions.Count(faction => faction.Kind == FactionKind.Npc));
        Assert.Equal(8, match.Armies.Count);
        Assert.All(match.Armies, army => Assert.Equal(100, army.Strength));
        Assert.Equal(92, match.Territories.Count(territory => territory.OwnerFactionId is null));
        Assert.All(match.Territories, territory =>
        {
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
        var service = new PrototypeMatchService();

        var match = service.CreateCardiffPrototype();
        var npcStartIndexes = match.Factions
            .Where(faction => faction.Kind == FactionKind.Npc)
            .Select(faction => match.Territories.Single(territory => territory.OwnerFactionId == faction.Id).Index)
            .Order()
            .ToArray();

        Assert.Equal(new[] { 22, 33, 44, 55, 66, 77 }, npcStartIndexes);
    }
}
