using Game.Domain;

namespace Game.Application;

public sealed class CardiffMatchService
{
    private readonly GameMapService mapService;
    private readonly CardiffPostcodeTerritoryRepository postcodeTerritoryRepository;
    private readonly CardiffTerritoryFeatureRepository territoryFeatureRepository;
    private readonly Random random;

    public CardiffMatchService(GameMapService mapService)
        : this(mapService, new CardiffPostcodeTerritoryRepository(), new CardiffTerritoryFeatureRepository(), Random.Shared)
    {
    }

    public CardiffMatchService(
        GameMapService mapService,
        CardiffPostcodeTerritoryRepository postcodeTerritoryRepository,
        CardiffTerritoryFeatureRepository territoryFeatureRepository,
        Random? random = null)
    {
        this.mapService = mapService;
        this.postcodeTerritoryRepository = postcodeTerritoryRepository;
        this.territoryFeatureRepository = territoryFeatureRepository;
        this.random = random ?? Random.Shared;
    }

    public MatchSnapshot CreateCardiffMatch()
    {
        return CreateCardiffMatch("cardiff-match");
    }

    public MatchSnapshot CreateCardiffMatch(string gameId)
    {
        var map = mapService.GetMap("cardiff");
        var factions = CreateFactions();
        var postcodeFeatures = postcodeTerritoryRepository.Load();
        var territoryFeatures = territoryFeatureRepository.Load();
        var startIndexes = CreateStartIndexes(postcodeFeatures.Count, factions.Select(faction => faction.Id).ToArray(), random);
        var factionByStartIndex = startIndexes.ToDictionary(pair => pair.Value, pair => pair.Key);
        var territories = postcodeFeatures
            .Select((feature, index) => CreateTerritory(
                feature,
                index,
                factionByStartIndex.GetValueOrDefault(index),
                territoryFeatures))
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
            GameId: gameId,
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

    private static IReadOnlyDictionary<string, int> CreateStartIndexes(int territoryCount, IReadOnlyList<string> factionIds, Random random)
    {
        if (territoryCount < 8)
        {
            throw new InvalidOperationException("Cardiff postcode territory data must contain at least 8 territories.");
        }

        if (factionIds.Count != 8)
        {
            throw new InvalidOperationException("Cardiff match setup expects exactly 8 factions.");
        }

        var availableIndexes = Enumerable.Range(0, territoryCount)
            .OrderBy(_ => random.Next())
            .Take(factionIds.Count)
            .ToArray();

        return factionIds
            .Select((factionId, index) => new { factionId, startIndex = availableIndexes[index] })
            .ToDictionary(item => item.factionId, item => item.startIndex);
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
        var routes = new Dictionary<string, MatchRouteDto>(StringComparer.Ordinal);
        for (var index = 0; index < territories.Count; index++)
        {
            foreach (var destinationIndex in NearestTerritoryIndexes(territories, index, count: 3))
            {
                var source = territories[index];
                var destination = territories[destinationIndex];
                var key = RouteKey(source.Id, destination.Id);
                if (routes.ContainsKey(key))
                {
                    continue;
                }

                var distanceSeconds = Math.Max(45, (int)Math.Round(CentroidDistance(source, destination) * 120_000));
                var transport = (index + destinationIndex) % 10 == 0
                    ? RouteTransport.Rail
                    : RouteTransport.Road;
                var result = MovementCalculator.Calculate(new MovementRoute(
                    BaseDistanceSeconds: distanceSeconds,
                    Transport: transport,
                    Terrain: (index + destinationIndex) % 8 == 0 ? RouteTerrain.Hills : RouteTerrain.Basic,
                    Barrier: (index + destinationIndex) % 15 == 0 ? RouteBarrier.BridgeOrTunnel : RouteBarrier.None));

                routes[key] = new MatchRouteDto(
                    SourceTerritoryId: source.Id,
                    DestinationTerritoryId: destination.Id,
                    Transport: transport,
                    EtaSeconds: result.EtaSeconds,
                    IsAllowed: result.IsAllowed);
            }
        }

        return routes.Values.ToList();
    }

    private static IEnumerable<int> NearestTerritoryIndexes(
        IReadOnlyList<MatchTerritoryDto> territories,
        int sourceIndex,
        int count) =>
        territories
            .Select((territory, index) => new
            {
                Index = index,
                Distance = index == sourceIndex
                    ? double.MaxValue
                    : CentroidDistance(territories[sourceIndex], territory)
            })
            .OrderBy(item => item.Distance)
            .ThenBy(item => item.Index)
            .Take(count)
            .Select(item => item.Index);

    private static double CentroidDistance(MatchTerritoryDto source, MatchTerritoryDto destination)
    {
        var sourceCenter = Centroid(source);
        var destinationCenter = Centroid(destination);
        var longitudeDelta = sourceCenter.Longitude - destinationCenter.Longitude;
        var latitudeDelta = sourceCenter.Latitude - destinationCenter.Latitude;
        return Math.Sqrt(longitudeDelta * longitudeDelta + latitudeDelta * latitudeDelta);
    }

    private static MapCoordinateDto Centroid(MatchTerritoryDto territory)
    {
        var coordinates = territory.BoundaryCoordinates;
        var count = coordinates.Count;
        if (count > 1 && coordinates[0] == coordinates[^1])
        {
            count--;
        }

        return new MapCoordinateDto(
            Longitude: coordinates.Take(count).Average(coordinate => coordinate.Longitude),
            Latitude: coordinates.Take(count).Average(coordinate => coordinate.Latitude));
    }

    private static string RouteKey(string sourceId, string destinationId) =>
        string.Compare(sourceId, destinationId, StringComparison.Ordinal) <= 0
            ? $"{sourceId}|{destinationId}"
            : $"{destinationId}|{sourceId}";

    private static string NormalizePostcode(string postcode) =>
        postcode
            .Trim()
            .ToLowerInvariant()
            .Replace(" ", "-", StringComparison.Ordinal);
}
