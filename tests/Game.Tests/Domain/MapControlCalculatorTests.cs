using Game.Domain;

namespace Game.Tests.Domain;

public sealed class MapControlCalculatorTests
{
    [Fact]
    public void RanksFactionsByControlledAreaPercentage()
    {
        var territories = new[]
        {
            new ControlledTerritory("t1", "player-1", 60),
            new ControlledTerritory("t2", "npc-1", 25),
            new ControlledTerritory("t3", null, 15)
        };
        var factions = new[]
        {
            new FactionStanding("player-1", "Player 1", 1),
            new FactionStanding("npc-1", "NPC 1", 0),
            new FactionStanding("npc-2", "NPC 2", 0)
        };

        var leaderboard = MapControlCalculator.CalculateLeaderboard(territories, factions);

        Assert.Collection(
            leaderboard,
            first =>
            {
                Assert.Equal(1, first.Rank);
                Assert.Equal("Player 1", first.FactionName);
                Assert.Equal(60.0, first.MapControlPercentage);
                Assert.False(first.IsEliminated);
                Assert.Equal(1, first.EliminationCount);
            },
            second =>
            {
                Assert.Equal(2, second.Rank);
                Assert.Equal("NPC 1", second.FactionName);
                Assert.Equal(25.0, second.MapControlPercentage);
                Assert.False(second.IsEliminated);
            },
            third =>
            {
                Assert.Equal(3, third.Rank);
                Assert.Equal("NPC 2", third.FactionName);
                Assert.True(third.IsEliminated);
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
