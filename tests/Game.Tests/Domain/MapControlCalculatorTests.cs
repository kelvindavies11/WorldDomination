using Game.Domain;

namespace Game.Tests.Domain;

public sealed class MapControlCalculatorTests
{
    [Fact]
    public void RanksFactionsByControlledAreaPercentage()
    {
        var territories = new[]
        {
            new ControlledTerritory("t1", "player-1", 60, new TerritoryStats(40, 0, 15, 25, 0, 0)),
            new ControlledTerritory("t2", "player-1", 10, new TerritoryStats(15, 0, 5, 10, 0, 0)),
            new ControlledTerritory("t3", "npc-1", 15, new TerritoryStats(8, 0, 6, 4, 0, 0)),
            new ControlledTerritory("t4", null, 15, new TerritoryStats(0, 0, 0, 0, 0, 0))
        };
        var factions = new[]
        {
            new FactionStanding("player-1", "Player 1", 1),
            new FactionStanding("npc-1", "NPC 1", 0),
            new FactionStanding("npc-2", "NPC 2", 0)
        };
        var armies = new[]
        {
            new ControlledArmy("player-1", 125),
            new ControlledArmy("npc-1", 40)
        };
        var routes = new[]
        {
            new ConnectedRoute("t1", "t2", IsAllowed: true),
            new ConnectedRoute("t2", "t3", IsAllowed: true)
        };

        var leaderboard = MapControlCalculator.CalculateLeaderboard(territories, factions, armies, routes);

        Assert.Collection(
            leaderboard,
            first =>
            {
                Assert.Equal(1, first.Rank);
                Assert.Equal("Player 1", first.FactionName);
                Assert.Equal(70.0, first.MapControlPercentage);
                Assert.False(first.IsEliminated);
                Assert.Equal(1, first.EliminationCount);
                Assert.Equal(2, first.TerritoryCount);
                Assert.Equal(55, first.Revenue);
                Assert.Equal(125, first.ArmyStrength);
                Assert.Equal(110, first.ArmyGrowth);
            },
            second =>
            {
                Assert.Equal(2, second.Rank);
                Assert.Equal("NPC 1", second.FactionName);
                Assert.Equal(15.0, second.MapControlPercentage);
                Assert.False(second.IsEliminated);
                Assert.Equal(1, second.TerritoryCount);
                Assert.Equal(8, second.Revenue);
                Assert.Equal(40, second.ArmyStrength);
                Assert.Equal(12, second.ArmyGrowth);
            },
            third =>
            {
                Assert.Equal(3, third.Rank);
                Assert.Equal("NPC 2", third.FactionName);
                Assert.True(third.IsEliminated);
                Assert.Equal(0, third.TerritoryCount);
                Assert.Equal(0, third.Revenue);
                Assert.Equal(0, third.ArmyStrength);
                Assert.Equal(0, third.ArmyGrowth);
            });
    }

    [Fact]
    public void DetectsVictoryAtOneHundredPercentControl()
    {
        var territories = new[]
        {
            new ControlledTerritory("t1", "player-1", 40),
            new ControlledTerritory("t2", "player-1", 60)
        };

        var winner = MapControlCalculator.FindWinner(territories);

        Assert.Equal("player-1", winner);
    }
}
