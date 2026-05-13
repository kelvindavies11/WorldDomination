using System.Collections.Concurrent;
using Game.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace Game.Application;

public sealed class CardiffMatchStateService
{
    private readonly CardiffMatchService matchService;
    private readonly WalesWestMatchService? walesWestMatchService;
    private readonly NorthWalesMatchService? northWalesMatchService;
    private readonly MidWalesMatchService? midWalesMatchService;
    private readonly SouthWalesMatchService? southWalesMatchService;
    private readonly IServiceScopeFactory? scopeFactory;

    // Per-game semaphores — one per game ID, created lazily
    private readonly ConcurrentDictionary<string, SemaphoreSlim> locks = new(StringComparer.OrdinalIgnoreCase);

    // In-memory fallback used by the test constructor (no DB)
    private readonly Dictionary<string, MatchSnapshot>? testSnapshots;
    private readonly Dictionary<string, DateTimeOffset>? testLastMovements;

    // Production constructor
    public CardiffMatchStateService(
        CardiffMatchService matchService,
        WalesWestMatchService walesWestMatchService,
        NorthWalesMatchService northWalesMatchService,
        MidWalesMatchService midWalesMatchService,
        SouthWalesMatchService southWalesMatchService,
        IServiceScopeFactory scopeFactory)
    {
        this.matchService = matchService;
        this.walesWestMatchService = walesWestMatchService;
        this.northWalesMatchService = northWalesMatchService;
        this.midWalesMatchService = midWalesMatchService;
        this.southWalesMatchService = southWalesMatchService;
        this.scopeFactory = scopeFactory;
    }

    // Test constructor — keeps old in-memory behaviour, no DB required
    public CardiffMatchStateService(MatchSnapshot snapshot)
    {
        matchService = new CardiffMatchService(new GameMapService());
        testSnapshots = new Dictionary<string, MatchSnapshot>(StringComparer.OrdinalIgnoreCase)
        {
            [snapshot.GameId] = snapshot
        };
        testLastMovements = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
    }

    public MatchSnapshot GetSnapshot(string gameId = "cardiff-match")
    {
        var normalizedId = NormalizeGameId(gameId);

        if (testSnapshots is not null)
        {
            lock (testSnapshots)
            {
                if (!testSnapshots.TryGetValue(normalizedId, out var s))
                {
                    s = CreateFreshSnapshot(normalizedId, lobbyService: null);
                    testSnapshots[normalizedId] = s;
                }
                return s;
            }
        }

        var semaphore = locks.GetOrAdd(normalizedId, _ => new SemaphoreSlim(1, 1));
        semaphore.Wait();
        try
        {
            using var scope = scopeFactory!.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IMatchStateRepository>();
            var lobby = scope.ServiceProvider.GetRequiredService<GameLobbyService>();

            var snapshot = repo.GetFullSnapshot(normalizedId);
            if (snapshot is not null) return snapshot;

            var fresh = CreateFreshSnapshot(normalizedId, lobby);
            repo.SaveMutableState(normalizedId, ExtractMutableState(fresh, ResolveMapId(normalizedId, lobby)));
            return fresh;
        }
        finally
        {
            semaphore.Release();
        }
    }

    public MatchSnapshot Update(string gameId, Func<MatchSnapshot, MatchSnapshot> update)
    {
        var normalizedId = NormalizeGameId(gameId);

        if (testSnapshots is not null)
        {
            lock (testSnapshots)
            {
                if (!testSnapshots.TryGetValue(normalizedId, out var s))
                    s = CreateFreshSnapshot(normalizedId, lobbyService: null);
                var updated = update(s);
                testSnapshots[normalizedId] = updated;
                return updated;
            }
        }

        var semaphore = locks.GetOrAdd(normalizedId, _ => new SemaphoreSlim(1, 1));
        semaphore.Wait();
        try
        {
            using var scope = scopeFactory!.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IMatchStateRepository>();
            var lobby = scope.ServiceProvider.GetRequiredService<GameLobbyService>();

            var current = repo.GetFullSnapshot(normalizedId)
                ?? CreateFreshSnapshot(normalizedId, lobby);

            var result = update(current);
            repo.SaveMutableState(normalizedId, ExtractMutableState(result, ResolveMapId(normalizedId, lobby)));
            return result;
        }
        finally
        {
            semaphore.Release();
        }
    }

    public MatchSnapshot Update(Func<MatchSnapshot, MatchSnapshot> update) =>
        Update("cardiff-match", update);

    public MatchSnapshot ClaimStartTerritory(string gameId, string territoryId, string factionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(territoryId);
        ArgumentException.ThrowIfNullOrWhiteSpace(factionId);

        return Update(gameId, snapshot =>
        {
            var territories = snapshot.Territories
                .Select(territory => territory.Id == territoryId
                    ? territory with { OwnerFactionId = factionId }
                    : territory)
                .ToList();
            var armies = snapshot.Armies
                .Where(army => army.TerritoryId != territoryId || army.FactionId != factionId)
                .Append(new MatchArmyDto(
                    Id: $"army-{factionId}-{territoryId}",
                    FactionId: factionId,
                    TerritoryId: territoryId,
                    Strength: 100))
                .ToList();
            var resources = CreateResources(snapshot.Factions, territories);
            var leaderboard = MapControlCalculator.CalculateLeaderboard(
                territories.Select(territory => new ControlledTerritory(
                    territory.Id,
                    territory.OwnerFactionId,
                    territory.AreaSquareKm,
                    territory.Stats)).ToArray(),
                snapshot.Factions.Select(faction => new FactionStanding(
                    faction.Id,
                    faction.Name,
                    EliminationCount: 0)).ToArray(),
                armies.Select(army => new ControlledArmy(army.FactionId, army.Strength)).ToArray(),
                snapshot.Routes.Select(route => new ConnectedRoute(
                    route.SourceTerritoryId,
                    route.DestinationTerritoryId,
                    route.IsAllowed)).ToArray(),
                resources.ToDictionary(resource => resource.FactionId, resource => resource.Revenue, StringComparer.Ordinal));

            return snapshot with
            {
                Territories = territories,
                Armies = armies,
                Leaderboard = leaderboard,
                Resources = resources
            };
        });
    }

    public void Invalidate(string gameId)
    {
        var normalizedId = NormalizeGameId(gameId);

        if (testSnapshots is not null)
        {
            lock (testSnapshots) { testSnapshots.Remove(normalizedId); }
            return;
        }

        var semaphore = locks.GetOrAdd(normalizedId, _ => new SemaphoreSlim(1, 1));
        semaphore.Wait();
        try
        {
            using var scope = scopeFactory!.CreateScope();
            scope.ServiceProvider.GetRequiredService<IMatchStateRepository>().DeleteMutableState(normalizedId);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public void TrackTerritoryMovement(string gameId)
    {
        var normalizedId = NormalizeGameId(gameId);

        if (testLastMovements is not null)
        {
            lock (testLastMovements) { testLastMovements[normalizedId] = DateTimeOffset.UtcNow; }
            return;
        }

        // No lock needed — last-write-wins for timestamps is acceptable
        using var scope = scopeFactory!.CreateScope();
        scope.ServiceProvider.GetRequiredService<IMatchStateRepository>()
            .TrackMovement(normalizedId, DateTimeOffset.UtcNow);
    }

    public DateTimeOffset? GetLastTerritoryMovementUtc(string gameId)
    {
        var normalizedId = NormalizeGameId(gameId);

        if (testLastMovements is not null)
        {
            lock (testLastMovements)
            {
                return testLastMovements.TryGetValue(normalizedId, out var v) ? v : null;
            }
        }

        using var scope = scopeFactory!.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IMatchStateRepository>()
            .GetLastMovement(normalizedId);
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private MatchSnapshot CreateFreshSnapshot(string gameId, GameLobbyService? lobbyService)
    {
        var matchSetup = lobbyService?.GetMatchSetup(gameId);
        if (matchSetup is null)
            return matchService.CreateCardiffMatch(gameId);

        if (string.Equals(matchSetup.MapArea, "wales-west", StringComparison.OrdinalIgnoreCase)
            && walesWestMatchService is not null)
            return walesWestMatchService.CreateWalesWestLobbyMatch(matchSetup);

        if (string.Equals(matchSetup.MapArea, "north-wales", StringComparison.OrdinalIgnoreCase)
            && northWalesMatchService is not null)
            return northWalesMatchService.CreateNorthWalesLobbyMatch(matchSetup);

        if (string.Equals(matchSetup.MapArea, "mid-wales", StringComparison.OrdinalIgnoreCase)
            && midWalesMatchService is not null)
            return midWalesMatchService.CreateMidWalesLobbyMatch(matchSetup);

        if (string.Equals(matchSetup.MapArea, "south-wales", StringComparison.OrdinalIgnoreCase)
            && southWalesMatchService is not null)
            return southWalesMatchService.CreateSouthWalesLobbyMatch(matchSetup);

        return matchService.CreateCardiffLobbyMatch(matchSetup);
    }

    private static string ResolveMapId(string gameId, GameLobbyService? lobbyService)
    {
        var mapArea = lobbyService?.GetMatchSetup(gameId)?.MapArea;
        return string.IsNullOrWhiteSpace(mapArea) ? "cardiff" : mapArea.Trim().ToLowerInvariant();
    }

    private static MatchSnapshotMutableState ExtractMutableState(MatchSnapshot snapshot, string mapId) => new(
        GameId: snapshot.GameId,
        MapId: mapId,
        MapArea: snapshot.MapArea,
        SnapshotGeneratedAtUtc: snapshot.SnapshotGeneratedAtUtc,
        GameState: snapshot.Game,
        TerritoryOwners: snapshot.Territories.ToDictionary(t => t.Id, t => t.OwnerFactionId),
        Armies: snapshot.Armies,
        Factions: snapshot.Factions,
        Routes: snapshot.Routes);

    private static IReadOnlyList<MatchFactionResourceDto> CreateResources(
        IReadOnlyList<MatchFactionDto> factions,
        IReadOnlyList<MatchTerritoryDto> territories) =>
        factions
            .Select(faction => new MatchFactionResourceDto(
                faction.Id,
                territories
                    .Where(territory => territory.OwnerFactionId == faction.Id)
                    .Sum(territory => territory.Stats.Economy)))
            .ToList();

    private static string NormalizeGameId(string gameId) =>
        string.Equals(gameId, "cardiff", StringComparison.OrdinalIgnoreCase)
            ? "cardiff-match"
            : gameId.Trim();
}
