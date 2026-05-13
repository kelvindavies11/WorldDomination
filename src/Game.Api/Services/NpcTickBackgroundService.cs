using Game.Api.Hubs;
using Game.Application;
using Microsoft.AspNetCore.SignalR;

namespace Game.Api.Services;

/// <summary>
/// Background service that drives NPC turns on a fixed interval.
/// Every tick it asks <see cref="NpcTurnService"/> to plan one neutral-capture
/// move per NPC faction in every active (started, not ended) game, then applies
/// each move and broadcasts the updated snapshot to connected clients.
/// NPC factions respect their <see cref="NpcNature"/>: Active factions act every
/// tick; Conservative act every other tick; Passive act every third tick.
/// </summary>
public sealed class NpcTickBackgroundService : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(5);

    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger<NpcTickBackgroundService> logger;

    public NpcTickBackgroundService(IServiceScopeFactory scopeFactory, ILogger<NpcTickBackgroundService> logger)
    {
        this.scopeFactory = scopeFactory;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Yield to let the host finish starting before the first tick
        await Task.Delay(TickInterval, stoppingToken).ConfigureAwait(false);

        using var timer = new PeriodicTimer(TickInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await TickAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "NPC tick encountered an error; skipping this tick.");
            }
        }
    }

    private async Task TickAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;

        // Singletons resolved from the DI container are safe to access directly
        var stateService = sp.GetRequiredService<CardiffMatchStateService>();
        var commandService = sp.GetRequiredService<PlayerTerritoryCommandService>();
        var hub = sp.GetRequiredService<IHubContext<MatchHub>>();

        // GameLobbyService is now scoped — resolve from the per-tick scope
        var lobbyService = sp.GetRequiredService<GameLobbyService>();
        var tickRepo = sp.GetRequiredService<INpcTickRepository>();

        var activeGames = lobbyService.ListAvailableGames()
            .Where(g => g.Status == "Started")
            .ToList();

        // Also tick the default Cardiff demo match (no lobby entry)
        var gameIds = activeGames.Select(g => g.Id)
            .Append("cardiff-match")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var gameId in gameIds)
        {
            try
            {
                await TickGameAsync(stateService, commandService, lobbyService, tickRepo, hub, gameId).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "NPC tick skipped game {GameId}.", gameId);
            }
        }
    }

    private async Task TickGameAsync(
        CardiffMatchStateService stateService,
        PlayerTerritoryCommandService commandService,
        GameLobbyService lobbyService,
        INpcTickRepository tickRepo,
        IHubContext<MatchHub> hub,
        string gameId)
    {
        var snapshot = stateService.GetSnapshot(gameId);
        if (!snapshot.Game.IsStarted || snapshot.Game.IsEnded)
        {
            return;
        }

        // Initialise inactivity tracking on the first tick for this game
        if (stateService.GetLastTerritoryMovementUtc(gameId) is null)
        {
            stateService.TrackTerritoryMovement(gameId);
        }

        // Increment per-faction tick counters and pass them to PlanMoves
        var gameTicks = IncrementTickCounts(gameId, snapshot, tickRepo);

        // --- NPC moves (nature-aware; may target neutral or enemy territories) ---
        var moves = NpcTurnService.PlanMoves(snapshot, gameTicks);
        var eliminatedFactionNames = new List<string>();
        foreach (var move in moves)
        {
            var moveResult = commandService.ExecuteNeutralCapture(move);

            // Broadcast immediately after each move so clients see territory
            // changes one-by-one rather than all at once at the end of the tick.
            if (moveResult.Accepted && moveResult.Snapshot is not null)
            {
                await hub.Clients
                    .Group(MatchHub.GroupName(gameId))
                    .SendAsync("SnapshotUpdated", moveResult.Snapshot)
                    .ConfigureAwait(false);
            }

            if (!string.IsNullOrWhiteSpace(moveResult.EliminatedFactionName))
            {
                eliminatedFactionNames.Add(moveResult.EliminatedFactionName);
            }
        }

        // --- Reinforcements for every faction (human + NPC) ---
        var reinforced = stateService.Update(gameId, current =>
        {
            var grants = ArmyReinforcementCalculator.Calculate(current);
            return ArmyReinforcementCalculator.Apply(current, grants);
        });

        // --- Inactivity stalemate check (5 minutes without any territory change) ---
        const int stalemateMinutes = 5;
        var lastMovement = stateService.GetLastTerritoryMovementUtc(gameId);
        if (lastMovement.HasValue && DateTimeOffset.UtcNow - lastMovement.Value > TimeSpan.FromMinutes(stalemateMinutes))
        {
            lobbyService.EndGame(gameId, null, null);
            var stale = stateService.Update(gameId, current => MatchVictoryEvaluator.ApplyStalemateEnd(current));

            await hub.Clients
                .Group(MatchHub.GroupName(gameId))
                .SendAsync("SnapshotUpdated", stale)
                .ConfigureAwait(false);

            await hub.Clients
                .Group(MatchHub.GroupName(gameId))
                .SendAsync("GameEnded", new { gameId, winnerFactionId = (string?)null, winnerFactionName = "Stalemate" })
                .ConfigureAwait(false);

            await hub.Clients.All.SendAsync("GamesUpdated", lobbyService.ListAvailableGames()).ConfigureAwait(false);
            return;
        }

        var winner = MatchVictoryEvaluator.TryGetWinner(reinforced);
        if (winner is not null)
        {
            lobbyService.EndGame(gameId, winner.FactionId, winner.FactionName);
            reinforced = stateService.Update(gameId, current => MatchVictoryEvaluator.ApplyVictory(current, winner));
        }

        // Broadcast the final snapshot (reinforcements always produce a change)
        await hub.Clients
            .Group(MatchHub.GroupName(gameId))
            .SendAsync("SnapshotUpdated", reinforced)
            .ConfigureAwait(false);

        // Broadcast any eliminations that occurred during NPC moves
        foreach (var eliminatedName in eliminatedFactionNames)
        {
            await hub.Clients
                .Group(MatchHub.GroupName(gameId))
                .SendAsync("FactionEliminated", new { eliminatedFactionName = eliminatedName, eliminatorFactionId = (string?)null })
                .ConfigureAwait(false);
        }

        if (winner is not null)
        {
            await hub.Clients
                .Group(MatchHub.GroupName(gameId))
                .SendAsync("GameEnded", new
                {
                    gameId,
                    winnerFactionId = winner.FactionId,
                    winnerFactionName = winner.FactionName
                })
                .ConfigureAwait(false);

            await hub.Clients.All.SendAsync("GamesUpdated", lobbyService.ListAvailableGames()).ConfigureAwait(false);
        }
    }

    private static Dictionary<string, int> IncrementTickCounts(string gameId, MatchSnapshot snapshot, INpcTickRepository tickRepo)
    {
        var gameTicks = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var faction in snapshot.Factions.Where(f => f.Kind == FactionKind.Npc))
        {
            var current = tickRepo.GetTickCount(gameId, faction.Id);
            var next = current + 1;
            tickRepo.SetTickCount(gameId, faction.Id, next);
            gameTicks[faction.Id] = next;
        }
        return gameTicks;
    }
}
