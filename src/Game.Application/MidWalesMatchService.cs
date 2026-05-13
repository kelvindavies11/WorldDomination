using Game.Domain;

namespace Game.Application;

public sealed class MidWalesMatchService
{
    private static readonly string[] HumanFactionColors =
    [
        "#1f8a70",
        "#2f6fbd",
        "#9f4fd4",
        "#d65a31",
        "#008c99",
        "#bc5090"
    ];

    private static readonly string[] NpcFactionColors =
    [
        "#c58a1a",
        "#b84a4a",
        "#5965a8",
        "#7d7f2a",
        "#008c99",
        "#8b5a2b",
        "#915f2a",
        "#4f7b55"
    ];

    private readonly GameMapService mapService;
    private readonly MidWalesPostcodeTerritoryRepository postcodeTerritoryRepository;
    private readonly WalesTerritoryFeatureRepository territoryFeatureRepository;
    private readonly Random random;

    public MidWalesMatchService(GameMapService mapService)
        : this(mapService, new MidWalesPostcodeTerritoryRepository(), new WalesTerritoryFeatureRepository(), Random.Shared)
    {
    }

    public MidWalesMatchService(
        GameMapService mapService,
        MidWalesPostcodeTerritoryRepository postcodeTerritoryRepository,
        WalesTerritoryFeatureRepository territoryFeatureRepository,
        Random? random = null)
    {
        this.mapService = mapService;
        this.postcodeTerritoryRepository = postcodeTerritoryRepository;
        this.territoryFeatureRepository = territoryFeatureRepository;
        this.random = random ?? Random.Shared;
    }

    public MatchSnapshot CreateMidWalesLobbyMatch(MatchSetupOptions setup)
    {
        var map = mapService.GetMap("mid-wales");
        var factions = CreateFactions(setup.HumanPlayers, setup.NpcFactions, setup.HumanPlayerNamesByFactionId);
        var postcodeFeatures = postcodeTerritoryRepository.Load();
        var territoryFeatures = territoryFeatureRepository.Load();
        var humanStartsByTerritoryId = setup.HumanStartTerritoriesByFactionId
            .ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.OrdinalIgnoreCase);
        var npcFactionIds = factions
            .Where(faction => faction.Kind == FactionKind.Npc)
            .Select(faction => faction.Id)
            .ToArray();
        var npcStartIndexes = CreateStartIndexes(
            postcodeFeatures.Count,
            npcFactionIds,
            new Random(DeterministicSeed(setup.GameId)));
        var npcFactionByStartIndex = npcStartIndexes.ToDictionary(pair => pair.Value, pair => pair.Key);
        var territories = postcodeFeatures
            .Select((feature, index) =>
            {
                var territoryId = $"postcode-{NormalizePostcode(feature.Postcode)}";
                var ownerFactionId = humanStartsByTerritoryId.GetValueOrDefault(territoryId)
                    ?? npcFactionByStartIndex.GetValueOrDefault(index);

                return CreateTerritory(feature, index, ownerFactionId, territoryFeatures);
            })
            .ToList();
        var armies = territories
            .Where(territory => !string.IsNullOrWhiteSpace(territory.OwnerFactionId))
            .Select(territory => new MatchArmyDto(
                Id: $"army-{territory.OwnerFactionId}-{territory.Id}",
                FactionId: territory.OwnerFactionId!,
                TerritoryId: territory.Id,
                Strength: 100))
            .ToList();
        var routes = CreateRoutes(territories);
        var resources = CreateInitialResources(factions, territories);
        var leaderboard = MapControlCalculator.CalculateLeaderboard(
            territories.Select(territory => new ControlledTerritory(
                territory.Id,
                territory.OwnerFactionId,
                territory.AreaSquareKm,
                territory.Stats)).ToArray(),
            factions.Select(faction => new FactionStanding(
                faction.Id,
                faction.Name,
                EliminationCount: 0)).ToArray(),
            armies.Select(army => new ControlledArmy(army.FactionId, army.Strength)).ToArray(),
            routes.Select(route => new ConnectedRoute(route.SourceTerritoryId, route.DestinationTerritoryId, route.IsAllowed)).ToArray(),
            resources.ToDictionary(resource => resource.FactionId, resource => resource.Revenue, StringComparer.Ordinal));

        return new MatchSnapshot(
            GameId: setup.GameId,
            MapArea: map.Name,
            SnapshotGeneratedAtUtc: DateTimeOffset.UtcNow,
            Game: new MatchGameStateDto(
                setup.Status,
                setup.IsStarted,
                setup.HumanPlayers,
                setup.MaxHumanPlayers,
                setup.NpcFactions,
                IsEnded: setup.IsEnded,
                WinningControlPercentage: setup.WinningControlPercentage,
                WinnerFactionId: setup.WinnerFactionId,
                WinnerFactionName: setup.WinnerFactionName),
            Map: map,
            Factions: factions,
            Territories: territories,
            Armies: armies,
            Routes: routes,
            Leaderboard: leaderboard,
            Resources: resources);
    }

    private static IReadOnlyList<MatchFactionResourceDto> CreateInitialResources(
        IReadOnlyList<MatchFactionDto> factions,
        IReadOnlyList<MatchTerritoryDto> territories) =>
        factions
            .Select(faction => new MatchFactionResourceDto(
                faction.Id,
                territories
                    .Where(territory => territory.OwnerFactionId == faction.Id)
                    .Sum(territory => territory.Stats.Economy)))
            .ToList();

    private static IReadOnlyList<MatchFactionDto> CreateFactions(
        int humanPlayers,
        int npcFactions,
        IReadOnlyDictionary<string, string>? playerNamesByFactionId = null)
    {
        if (humanPlayers < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(humanPlayers), "A match needs at least one human player.");
        }

        if (npcFactions < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(npcFactions), "NPC faction count cannot be negative.");
        }

        var factions = Enumerable.Range(1, humanPlayers)
            .Select(index =>
            {
                var factionId = $"human-{index}";
                var name = playerNamesByFactionId?.GetValueOrDefault(factionId) ?? $"Player {index}";
                return new MatchFactionDto(
                    factionId,
                    name,
                    FactionKind.Human,
                    HumanFactionColors[(index - 1) % HumanFactionColors.Length]);
            })
            .Concat(Enumerable.Range(1, npcFactions)
                .Select(index =>
                {
                    var nature = (NpcNature)((index - 1) % 3);
                    return new MatchFactionDto(
                        $"npc-{index}",
                        $"NPC {index} ({nature})",
                        FactionKind.Npc,
                        NpcFactionColors[(index - 1) % NpcFactionColors.Length],
                        nature);
                }))
            .ToList();

        return factions;
    }

    private static IReadOnlyDictionary<string, int> CreateStartIndexes(int territoryCount, IReadOnlyList<string> factionIds, Random random)
    {
        if (territoryCount < factionIds.Count)
        {
            throw new InvalidOperationException($"Mid Wales postcode territory data must contain at least {factionIds.Count} territories.");
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
                var distanceSeconds = Math.Max(45, (int)Math.Round(CentroidDistance(source, destination) * 120_000));
                var transport = (index + destinationIndex) % 10 == 0
                    ? RouteTransport.Rail
                    : RouteTransport.Road;
                var result = MovementCalculator.Calculate(new MovementRoute(
                    BaseDistanceSeconds: distanceSeconds,
                    Transport: transport,
                    Terrain: (index + destinationIndex) % 8 == 0 ? RouteTerrain.Hills : RouteTerrain.Basic,
                    Barrier: (index + destinationIndex) % 15 == 0 ? RouteBarrier.BridgeOrTunnel : RouteBarrier.None));

                routes[$"{source.Id}|{destination.Id}"] = new MatchRouteDto(
                    SourceTerritoryId: source.Id,
                    DestinationTerritoryId: destination.Id,
                    Transport: transport,
                    EtaSeconds: result.EtaSeconds,
                    IsAllowed: result.IsAllowed);
                routes[$"{destination.Id}|{source.Id}"] = new MatchRouteDto(
                    SourceTerritoryId: destination.Id,
                    DestinationTerritoryId: source.Id,
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

    private static int DeterministicSeed(string value)
    {
        unchecked
        {
            var hash = (int)2166136261;
            foreach (var character in value)
            {
                hash ^= character;
                hash *= 16777619;
            }

            return hash;
        }
    }

    private static string NormalizePostcode(string postcode) =>
        postcode
            .Trim()
            .ToLowerInvariant()
            .Replace(" ", "-", StringComparison.Ordinal);
}
