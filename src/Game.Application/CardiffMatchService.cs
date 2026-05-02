using Game.Domain;

namespace Game.Application;

public sealed class CardiffMatchService
{
    private readonly GameMapService mapService;
    private readonly CardiffPostcodeTerritoryRepository postcodeTerritoryRepository;

    public CardiffMatchService(GameMapService mapService)
        : this(mapService, new CardiffPostcodeTerritoryRepository())
    {
    }

    public CardiffMatchService(
        GameMapService mapService,
        CardiffPostcodeTerritoryRepository postcodeTerritoryRepository)
    {
        this.mapService = mapService;
        this.postcodeTerritoryRepository = postcodeTerritoryRepository;
    }

    public MatchSnapshot CreateCardiffMatch()
    {
        var map = mapService.GetMap("cardiff");
        var factions = CreateFactions();
        var postcodeFeatures = postcodeTerritoryRepository.Load();
        var startIndexes = CreateStartIndexes(postcodeFeatures.Count);
        var factionByStartIndex = startIndexes.ToDictionary(pair => pair.Value, pair => pair.Key);
        var territories = postcodeFeatures
            .Select((feature, index) => CreateTerritory(feature, index, factionByStartIndex.GetValueOrDefault(index)))
            .ToList();
        var armies = startIndexes
            .Select(pair => new MatchArmyDto(
                Id: $"army-{pair.Key}",
                FactionId: pair.Key,
                TerritoryId: territories[pair.Value].Id,
                Strength: 100))
            .ToList();
        var routes = CreateRoutes(territories);
        var leaderboard = MapControlCalculator.CalculateLeaderboard(
            territories.Select(territory => new ControlledTerritory(
                territory.Id,
                territory.OwnerFactionId,
                territory.AreaSquareKm)).ToArray(),
            factions.Select(faction => new FactionStanding(
                faction.Id,
                faction.Name,
                EliminationCount: 0)).ToArray());

        return new MatchSnapshot(
            GameId: "cardiff-match",
            MapArea: map.Name,
            Map: map,
            Factions: factions,
            Territories: territories,
            Armies: armies,
            Routes: routes,
            Leaderboard: leaderboard);
    }

    private static IReadOnlyList<MatchFactionDto> CreateFactions() =>
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

    private static IReadOnlyDictionary<string, int> CreateStartIndexes(int territoryCount)
    {
        if (territoryCount < 8)
        {
            throw new InvalidOperationException("Cardiff postcode territory data must contain at least 8 territories.");
        }

        return new Dictionary<string, int>
        {
            ["human-1"] = 0,
            ["human-2"] = territoryCount - 1,
            ["npc-1"] = territoryCount / 7,
            ["npc-2"] = territoryCount * 2 / 7,
            ["npc-3"] = territoryCount * 3 / 7,
            ["npc-4"] = territoryCount * 4 / 7,
            ["npc-5"] = territoryCount * 5 / 7,
            ["npc-6"] = territoryCount * 6 / 7
        };
    }

    private static MatchTerritoryDto CreateTerritory(
        PostcodeTerritoryFeature feature,
        int index,
        string? ownerFactionId)
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

        var populationSupport = int.TryParse(feature.Postcode[^2..], out var postcodeNumber)
            ? postcodeNumber % 12
            : index % 12;

        var adjustedFeatures = features with
        {
            PopulationSupport = populationSupport
        };

        return new MatchTerritoryDto(
            Id: $"postcode-{NormalizePostcode(feature.Postcode)}",
            Index: index,
            Name: feature.Name,
            AreaSquareKm: adjustedFeatures.AreaSquareKm,
            OwnerFactionId: ownerFactionId,
            Stats: TerritoryStatCalculator.Calculate(adjustedFeatures, Ruleset.Default),
            BoundaryCoordinates: feature.BoundaryCoordinates,
            Postcode: feature.Postcode);
    }

    private static IReadOnlyList<MatchRouteDto> CreateRoutes(IReadOnlyList<MatchTerritoryDto> territories)
    {
        return Enumerable.Range(0, territories.Count - 1)
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

                return new MatchRouteDto(
                    SourceTerritoryId: territories[index].Id,
                    DestinationTerritoryId: territories[index + 1].Id,
                    Transport: transport,
                    EtaSeconds: result.EtaSeconds,
                    IsAllowed: result.IsAllowed);
            })
            .ToList();
    }

    private static string NormalizePostcode(string postcode) =>
        postcode
            .Trim()
            .ToLowerInvariant()
            .Replace(" ", "-", StringComparison.Ordinal);
}
