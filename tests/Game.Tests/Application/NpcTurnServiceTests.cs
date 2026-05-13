using Game.Application;
using Game.Domain;

namespace Game.Tests.Application;

public sealed class NpcTurnServiceTests
{
    // -----------------------------------------------------------------------
    // PlanMoves — basic move selection
    // -----------------------------------------------------------------------

    [Fact]
    public void PlansMoveForNpcIntoAdjacentNeutralTerritory()
    {
        var snapshot = CreateSnapshot();

        var moves = NpcTurnService.PlanMoves(snapshot);

        var move = Assert.Single(moves);
        Assert.Equal("npc-1", move.PlayerFactionId);
        Assert.Equal("npc-source", move.SourceTerritoryId);
        Assert.Equal("neutral", move.TargetTerritoryId);
        Assert.InRange(move.Strength, 1, 100);
    }

    [Fact]
    public void ReturnsNoMovesWhenGameIsNotStarted()
    {
        var snapshot = CreateSnapshot() with
        {
            Game = new MatchGameStateDto("Lobby", false, 1, 1, 1)
        };

        var moves = NpcTurnService.PlanMoves(snapshot);

        Assert.Empty(moves);
    }

    [Fact]
    public void ReturnsNoMovesWhenNpcHasNoArmy()
    {
        var snapshot = CreateSnapshot();
        // Remove the NPC army
        snapshot = snapshot with
        {
            Armies = snapshot.Armies.Where(a => a.FactionId != "npc-1").ToList()
        };

        var moves = NpcTurnService.PlanMoves(snapshot);

        Assert.Empty(moves);
    }

    [Fact]
    public void ActiveNpcAttacksAdjacentEnemyWhenNoNeutralAvailable()
    {
        // Make the only adjacent territory an enemy-owned one
        var snapshot = CreateSnapshot();
        snapshot = snapshot with
        {
            Territories = snapshot.Territories
                .Select(t => t.Id == "neutral" ? t with { OwnerFactionId = "human-1" } : t)
                .ToList(),
            Armies = snapshot.Armies
                .Append(new MatchArmyDto("army-human-on-neutral", "human-1", "neutral", 10))
                .ToList()
        };

        // Active NPC (null nature = Active) should attack the enemy territory
        var moves = NpcTurnService.PlanMoves(snapshot);

        var move = Assert.Single(moves);
        Assert.Equal("npc-1", move.PlayerFactionId);
        Assert.Equal("neutral", move.TargetTerritoryId);
    }

    [Fact]
    public void PassiveNpcDoesNotAttackAdjacentEnemyTerritories()
    {
        // Set npc-1 to Passive and make the only adjacent territory enemy-owned
        var snapshot = CreateSnapshot();
        snapshot = snapshot with
        {
            Factions = snapshot.Factions
                .Select(f => f.Id == "npc-1" ? f with { Nature = NpcNature.Passive } : f)
                .ToList(),
            Territories = snapshot.Territories
                .Select(t => t.Id == "neutral" ? t with { OwnerFactionId = "human-1" } : t)
                .ToList()
        };

        var moves = NpcTurnService.PlanMoves(snapshot);

        // Passive NPC never attacks enemies
        Assert.Empty(moves);
    }

    [Fact]
    public void PlansOneMovePer_NpcFaction_WhenMultipleNpcsPresent()
    {
        var snapshot = CreateSnapshotWithTwoNpcs();

        var moves = NpcTurnService.PlanMoves(snapshot);

        // One move per NPC
        Assert.Equal(2, moves.Count);
        Assert.Contains(moves, m => m.PlayerFactionId == "npc-1");
        Assert.Contains(moves, m => m.PlayerFactionId == "npc-2");
    }

    [Fact]
    public void MoveSendStrengthIsRandomisedBetweenOneAndArmySize()
    {
        var snapshot = CreateSnapshot();

        var moves = NpcTurnService.PlanMoves(snapshot);

        Assert.All(moves, m => Assert.InRange(m.Strength, 1, 100));
    }

    // -----------------------------------------------------------------------
    // Integration: NpcTurnService + PlayerTerritoryCommandService
    // -----------------------------------------------------------------------

    [Fact]
    public void AppliedMoveTransfersNeutralTerritoryOwnershipToNpc()
    {
        var snapshot = CreateSnapshot();
        var state = new CardiffMatchStateService(snapshot);
        var commandService = new PlayerTerritoryCommandService(state);

        var moves = NpcTurnService.PlanMoves(snapshot);
        var result = commandService.ExecuteTakeover(moves[0]);

        Assert.True(result.Accepted, result.Error);
        Assert.NotNull(result.Snapshot);
        var captured = result.Snapshot.Territories.Single(t => t.Id == "neutral");
        Assert.Equal("npc-1", captured.OwnerFactionId);
    }

    [Fact]
    public void ConservativeNpcAttacksEnemyOnlyWhenAttackerArmyIsStronger()
    {
        // Set npc-1 to Conservative; give the enemy a small garrison so attacker is stronger
        var snapshot = CreateSnapshot();
        snapshot = snapshot with
        {
            Factions = snapshot.Factions
                .Select(f => f.Id == "npc-1" ? f with { Nature = NpcNature.Conservative } : f)
                .ToList(),
            Territories = snapshot.Territories
                .Select(t => t.Id == "neutral" ? t with { OwnerFactionId = "human-1" } : t)
                .ToList(),
            Armies = snapshot.Armies
                .Append(new MatchArmyDto("army-human-on-neutral", "human-1", "neutral", 10))
                .ToList()
        };

        // npc-1 has 100 vs 10 defenders → should attack
        var moves = NpcTurnService.PlanMoves(snapshot);

        var move = Assert.Single(moves);
        Assert.Equal("npc-1", move.PlayerFactionId);
        Assert.Equal("neutral", move.TargetTerritoryId);
    }

    [Fact]
    public void ConservativeNpcDoesNotAttackStrongerEnemy()
    {
        // Set npc-1 to Conservative; give enemy a large garrison so defender is stronger
        var snapshot = CreateSnapshot();
        snapshot = snapshot with
        {
            Factions = snapshot.Factions
                .Select(f => f.Id == "npc-1" ? f with { Nature = NpcNature.Conservative } : f)
                .ToList(),
            Territories = snapshot.Territories
                .Select(t => t.Id == "neutral" ? t with { OwnerFactionId = "human-1" } : t)
                .ToList(),
            Armies = snapshot.Armies
                .Append(new MatchArmyDto("army-human-on-neutral", "human-1", "neutral", 200))
                .ToList()
        };

        // npc-1 has 100 vs 200 defenders → should NOT attack
        var moves = NpcTurnService.PlanMoves(snapshot);

        Assert.Empty(moves);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static MatchSnapshot CreateSnapshot()
    {
        var factions = new[]
        {
            new MatchFactionDto("human-1", "Player 1", FactionKind.Human, "#1f8a70"),
            new MatchFactionDto("npc-1", "NPC 1", FactionKind.Npc, "#c58a1a")
        };
        var territories = new[]
        {
            Territory("human-source", "human-1", 50),
            Territory("npc-source", "npc-1", 50),
            Territory("neutral", null, 25),
        };
        var armies = new[]
        {
            new MatchArmyDto("army-human-1", "human-1", "human-source", 100),
            new MatchArmyDto("army-npc-1", "npc-1", "npc-source", 100)
        };
        var routes = new[]
        {
            new MatchRouteDto("npc-source", "neutral", RouteTransport.Road, 70, IsAllowed: true),
            new MatchRouteDto("human-source", "neutral", RouteTransport.Road, 70, IsAllowed: true)
        };
        var leaderboard = MapControlCalculator.CalculateLeaderboard(
            territories.Select(t => new ControlledTerritory(t.Id, t.OwnerFactionId, t.AreaSquareKm)).ToArray(),
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
            Routes: routes,
            Leaderboard: leaderboard);
    }

    private static MatchSnapshot CreateSnapshotWithTwoNpcs()
    {
        var factions = new[]
        {
            new MatchFactionDto("human-1", "Player 1", FactionKind.Human, "#1f8a70"),
            new MatchFactionDto("npc-1", "NPC 1", FactionKind.Npc, "#c58a1a"),
            new MatchFactionDto("npc-2", "NPC 2", FactionKind.Npc, "#b84a4a")
        };
        var territories = new[]
        {
            Territory("human-source", "human-1", 30),
            Territory("npc1-source", "npc-1", 30),
            Territory("npc2-source", "npc-2", 30),
            Territory("neutral-1", null, 25),
            Territory("neutral-2", null, 25)
        };
        var armies = new[]
        {
            new MatchArmyDto("army-human", "human-1", "human-source", 100),
            new MatchArmyDto("army-npc1", "npc-1", "npc1-source", 100),
            new MatchArmyDto("army-npc2", "npc-2", "npc2-source", 100)
        };
        var routes = new[]
        {
            new MatchRouteDto("npc1-source", "neutral-1", RouteTransport.Road, 70, IsAllowed: true),
            new MatchRouteDto("npc2-source", "neutral-2", RouteTransport.Road, 70, IsAllowed: true)
        };
        var leaderboard = MapControlCalculator.CalculateLeaderboard(
            territories.Select(t => new ControlledTerritory(t.Id, t.OwnerFactionId, t.AreaSquareKm)).ToArray(),
            factions.Select(f => new FactionStanding(f.Id, f.Name, 0)).ToArray());

        return new MatchSnapshot(
            GameId: "test-game",
            MapArea: "Cardiff",
            SnapshotGeneratedAtUtc: DateTimeOffset.UtcNow,
            Game: new MatchGameStateDto("Started", true, 1, 1, 2),
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
            Stats: new TerritoryStats(Economy: 10, Defense: 0, Mobility: 5, StrategicValue: 2, RevenuePerTick: 0, ArmyGrowthPerTick: 0),
            Postcode: null,
            Features: TerritoryFeatureSummary.Empty,
            BoundaryCoordinates: []);

    // -----------------------------------------------------------------------
    // ShouldActThisTick — nature rules
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(1), InlineData(2), InlineData(3), InlineData(6)]
    public void ActiveNpcActsEveryTick(int tick)
    {
        Assert.True(NpcTurnService.ShouldActThisTick(NpcNature.Active, tick));
    }

    [Theory]
    [InlineData(1, true), InlineData(2, false), InlineData(3, true), InlineData(4, false)]
    public void ConservativeNpcActsEveryOtherTick(int tick, bool expected)
    {
        Assert.Equal(expected, NpcTurnService.ShouldActThisTick(NpcNature.Conservative, tick));
    }

    [Theory]
    [InlineData(1, true), InlineData(2, false), InlineData(3, false),
     InlineData(4, true), InlineData(5, false), InlineData(6, false), InlineData(7, true)]
    public void PassiveNpcActsEveryThirdTick(int tick, bool expected)
    {
        Assert.Equal(expected, NpcTurnService.ShouldActThisTick(NpcNature.Passive, tick));
    }

    [Fact]
    public void NullNatureIsTreeedAsActive()
    {
        Assert.True(NpcTurnService.ShouldActThisTick(null, 2));
    }

    // -----------------------------------------------------------------------
    // PlanMoves — nature-based skipping
    // -----------------------------------------------------------------------

    [Fact]
    public void PassiveNpcIsSkippedOnTick2()
    {
        var snapshot = CreateSnapshotWithNature(NpcNature.Passive);
        var tickCounts = new Dictionary<string, int> { ["npc-1"] = 2 };

        var moves = NpcTurnService.PlanMoves(snapshot, tickCounts);

        Assert.Empty(moves);
    }

    [Fact]
    public void PassiveNpcActsOnTick1()
    {
        var snapshot = CreateSnapshotWithNature(NpcNature.Passive);
        var tickCounts = new Dictionary<string, int> { ["npc-1"] = 1 };

        var moves = NpcTurnService.PlanMoves(snapshot, tickCounts);

        Assert.Single(moves);
    }

    [Fact]
    public void PassiveNpcActsOnTick4()
    {
        var snapshot = CreateSnapshotWithNature(NpcNature.Passive);
        var tickCounts = new Dictionary<string, int> { ["npc-1"] = 4 };

        var moves = NpcTurnService.PlanMoves(snapshot, tickCounts);

        Assert.Single(moves);
    }

    [Fact]
    public void ConservativeNpcIsSkippedOnEvenTick()
    {
        var snapshot = CreateSnapshotWithNature(NpcNature.Conservative);
        var tickCounts = new Dictionary<string, int> { ["npc-1"] = 2 };

        var moves = NpcTurnService.PlanMoves(snapshot, tickCounts);

        Assert.Empty(moves);
    }

    [Fact]
    public void ConservativeNpcActsOnOddTick()
    {
        var snapshot = CreateSnapshotWithNature(NpcNature.Conservative);
        var tickCounts = new Dictionary<string, int> { ["npc-1"] = 3 };

        var moves = NpcTurnService.PlanMoves(snapshot, tickCounts);

        Assert.Single(moves);
    }

    private static MatchSnapshot CreateSnapshotWithNature(NpcNature nature)
    {
        var snapshot = CreateSnapshot();
        return snapshot with
        {
            Factions = snapshot.Factions
                .Select(f => f.Kind == FactionKind.Npc ? f with { Nature = nature } : f)
                .ToList()
        };
    }
}
