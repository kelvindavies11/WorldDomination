using Game.Domain;

namespace Game.Application;

public sealed class CardiffMatchService
{
    private readonly GameMapService mapService;
    private readonly CardiffPostcodeTerritoryRepository postcodeTerritoryRepository;
    private readonly CardiffTerritoryFeatureRepository territoryFeatureRepository;

    public CardiffMatchService(GameMapService mapService)
        : this(mapService, new CardiffPostcodeTerritoryRepository(), new CardiffTerritoryFeatureRepository())
    {
    }

    public CardiffMatchService(
        GameMapService mapService,
        CardiffPostcodeTerritoryRepository postcodeTerritoryRepository,
        CardiffTerritoryFeatureRepository territoryFeatureRepository)
    {
        this.mapService = mapService;
        this.postcodeTerritoryRepository = postcodeTerritoryRepository;
        this.territoryFeatureRepository = territoryFeatureRepository;
    }

    public MatchSnapshot CreateCardiffMatch()
    {
        var map = mapService.GetMap("cardiff");
        var factions = CreateFactions();
        var postcodeFeatures = postcodeTerritoryRepository.Load();
        var territoryFeatures = territoryFeatureRepository.Load();
        var startIndexes = CreateStartIndexes(postcodeFeatures.Count);
        var factionByStartIndex = startIndexes.ToDictionary(pair => pair.Value, pair => pair.Key);
        var territories = postcodeFeatures
            .Select((feature, index) => CreateTerritory(
                feature,
                index,
                factionByStartIndex.GetValueOrDefault(index),
                territoryFeatures))
            .ToList();
        map = map with
        {
            Territories = territories
                .Select(territory => new MapTerritoryDto(
                    territory.Id,
                    territory.Name,
                    territory.Postcode ?? territory.Name,
                    territory.Stats,
                    territory.Features,
                    territory.BoundaryCoordinates))
                .ToList()
        };
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
            SnapshotGeneratedAtUtc: DateTimeOffset.UtcNow,
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
        string? ownerFactionId,
        IReadOnlyDictionary<string, TerritoryFeatureSummary> territoryFeatures)
    {
        if (!territoryFeatures.TryGetValue(feature.Postcode, out var features))
        {
            throw new InvalidOperationException($"No persisted OSM feature summary exists for postcode sector '{feature.Postcode}'.");
        }

        return new MatchTerritoryDto(
            Id: $"postcode-{NormalizePostcode(feature.Postcode)}",
            Index: index,
            Name: feature.Name,
            AreaSquareKm: features.AreaSquareKm,
            OwnerFactionId: ownerFactionId,
            Stats: TerritoryStatCalculator.Calculate(features, Ruleset.Default),
            Postcode: feature.Postcode,
            Features: features,
            BoundaryCoordinates: feature.BoundaryCoordinates);
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
