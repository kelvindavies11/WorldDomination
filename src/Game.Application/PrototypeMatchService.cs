using Game.Domain;

namespace Game.Application;

public sealed class PrototypeMatchService
{
    public PrototypeMatchSnapshot CreateCardiffPrototype()
    {
        var factions = CreateFactions();
        var startIndexes = new Dictionary<string, int>
        {
            ["human-1"] = 0,
            ["human-2"] = 11,
            ["npc-1"] = 22,
            ["npc-2"] = 33,
            ["npc-3"] = 44,
            ["npc-4"] = 55,
            ["npc-5"] = 66,
            ["npc-6"] = 77
        };
        var factionByStartIndex = startIndexes.ToDictionary(pair => pair.Value, pair => pair.Key);
        var territories = Enumerable.Range(0, 100)
            .Select(index => CreateTerritory(index, factionByStartIndex.GetValueOrDefault(index)))
            .ToList();
        var armies = startIndexes
            .Select(pair => new PrototypeArmyDto(
                Id: $"army-{pair.Key}",
                FactionId: pair.Key,
                TerritoryId: $"territory-{pair.Value:000}",
                Strength: 100))
            .ToList();
        var routes = CreateRoutes();
        var leaderboard = MapControlCalculator.CalculateLeaderboard(
            territories.Select(territory => new ControlledTerritory(
                territory.Id,
                territory.OwnerFactionId,
                territory.AreaSquareKm)).ToArray(),
            factions.Select(faction => new FactionStanding(
                faction.Id,
                faction.Name,
                EliminationCount: 0)).ToArray());

        return new PrototypeMatchSnapshot(
            GameId: "cardiff-prototype",
            MapArea: "Cardiff",
            Factions: factions,
            Territories: territories,
            Armies: armies,
            Routes: routes,
            Leaderboard: leaderboard);
    }

    private static IReadOnlyList<PrototypeFactionDto> CreateFactions() =>
    [
        new("human-1", "Player 1", FactionKind.Human, "#1f8a70"),
        new("human-2", "Player 2", FactionKind.Human, "#2f6fbd"),
        new("npc-1", "NPC 1", FactionKind.Npc, "#c58a1a"),
        new("npc-2", "NPC 2", FactionKind.Npc, "#b84a4a"),
        new("npc-3", "NPC 3", FactionKind.Npc, "#5965a8"),
        new("npc-4", "NPC 4", FactionKind.Npc, "#7d7f2a"),
        new("npc-5", "NPC 5", FactionKind.Npc, "#008c99"),
        new("npc-6", "NPC 6", FactionKind.Npc, "#8b5a2b")
    ];

    private static PrototypeTerritoryDto CreateTerritory(int index, string? ownerFactionId)
    {
        var features = new TerritoryFeatureSummary(
            Factories: index % 6,
            Shops: (index * 2) % 11,
            CommercialAreas: (index + 3) % 8,
            Offices: (index * 3) % 10,
            IndustrialSites: index % 5,
            FarmlandOrResources: (index + 1) % 4,
            PopulationSupport: (index * 5) % 12,
            Mountains: index % 17 == 0 ? 2 : 0,
            Hills: index % 4,
            MilitarySites: index % 29 == 0 ? 1 : 0,
            CastlesOrForts: index % 31 == 0 ? 1 : 0,
            GovernmentSites: index % 9,
            Chokepoints: index % 7,
            UrbanDensity: (index * 4) % 11,
            Roads: (index * 7) % 12,
            Railways: index % 5,
            BridgesOrTunnels: index % 6,
            Airports: index is 18 or 72 ? 1 : 0,
            Ports: index is 8 or 64 ? 1 : 0,
            Connections: 2 + index % 5,
            AreaSquareKm: 0.8 + index % 5 * 0.2,
            SpecialFeatures: index % 13 == 0 ? 2 : 0);

        return new PrototypeTerritoryDto(
            Id: $"territory-{index:000}",
            Index: index,
            Name: $"Cardiff Sector {index + 1}",
            AreaSquareKm: features.AreaSquareKm,
            OwnerFactionId: ownerFactionId,
            Stats: TerritoryStatCalculator.Calculate(features, Ruleset.Default));
    }

    private static IReadOnlyList<PrototypeRouteDto> CreateRoutes()
    {
        return Enumerable.Range(0, 99)
            .Select(index =>
            {
                var transport = index % 10 == 0
                    ? RouteTransport.Rail
                    : RouteTransport.Road;
                var result = MovementCalculator.Calculate(new MovementRoute(
                    BaseDistanceSeconds: 90 + index % 20,
                    Transport: transport,
                    Terrain: index % 8 == 0 ? RouteTerrain.Hills : RouteTerrain.Basic,
                    Barrier: index % 15 == 0 ? RouteBarrier.BridgeOrTunnel : RouteBarrier.None));

                return new PrototypeRouteDto(
                    SourceTerritoryId: $"territory-{index:000}",
                    DestinationTerritoryId: $"territory-{index + 1:000}",
                    Transport: transport,
                    EtaSeconds: result.EtaSeconds,
                    IsAllowed: result.IsAllowed);
            })
            .ToList();
    }
}
