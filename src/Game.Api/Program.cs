using Game.Api.Hubs;
using Game.Api.Services;
using Game.Application;
using Game.Infrastructure;
using Game.Infrastructure.Repositories;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// EF Core — SQLite
builder.Services.AddDbContext<GameDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("GameDb") ?? "Data Source=game.db"));

// Scoped EF-backed repository implementations
builder.Services.AddScoped<IGameRepository, EfGameRepository>();
builder.Services.AddScoped<IMatchStateRepository, EfMatchStateRepository>();
builder.Services.AddScoped<INpcTickRepository, EfNpcTickRepository>();

builder.Services.AddSingleton<CardiffMatchService>();
builder.Services.AddSingleton<WalesWestMatchService>();
builder.Services.AddSingleton<NorthWalesMatchService>();
builder.Services.AddSingleton<MidWalesMatchService>();
builder.Services.AddSingleton<SouthWalesMatchService>();
builder.Services.AddSingleton<CardiffMatchStateService>();
builder.Services.AddSingleton<PlayerTerritoryCommandService>();
builder.Services.AddScoped<GameLobbyService>();
builder.Services.AddSingleton<GameMapService>();
builder.Services.AddSignalR();
builder.Services.AddHostedService<NpcTickBackgroundService>();

var app = builder.Build();

// Apply migrations and seed static data on startup
using (var startupScope = app.Services.CreateScope())
{
    var db = startupScope.ServiceProvider.GetRequiredService<GameDbContext>();
    await db.Database.EnsureCreatedAsync();
}
await StaticDataSeeder.SeedAsync(app.Services);

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/matches/{gameId}", (string gameId, CardiffMatchStateService service) =>
    Results.Ok(service.GetSnapshot(gameId)));

app.MapPost("/api/matches/{gameId}/movements", async (string gameId, SendArmyCommand request, PlayerTerritoryCommandService service, CardiffMatchStateService stateService, GameLobbyService lobbyService, IHubContext<MatchHub> hub) =>
{
    var before = stateService.GetSnapshot(gameId);
    var targetBefore = before.Territories.SingleOrDefault(territory => territory.Id == request.TargetTerritoryId);
    var actionType = targetBefore?.OwnerFactionId == request.PlayerFactionId ? "reinforce" : "attack";
    var result = service.SendArmy(request with { GameId = gameId });
    if (!result.Accepted)
        return Results.BadRequest(new { error = result.Error });

    var snapshot = ResolveWinner(stateService, lobbyService, gameId);
    var targetAfter = snapshot.Territories.SingleOrDefault(territory => territory.Id == request.TargetTerritoryId);
    await BroadcastTerritoryActionResolved(hub, gameId, request.SourceTerritoryId, request.TargetTerritoryId, actionType, request.Strength, targetAfter?.OwnerFactionId);
    await hub.Clients.Group(MatchHub.GroupName(gameId)).SendAsync("SnapshotUpdated", snapshot);

    if (!string.IsNullOrWhiteSpace(result.EliminatedFactionName))
    {
        await hub.Clients.Group(MatchHub.GroupName(gameId)).SendAsync("FactionEliminated", new
        {
            eliminatedFactionName = result.EliminatedFactionName,
            eliminatorFactionId = request.PlayerFactionId
        });
    }

    if (snapshot.Game.IsEnded && !string.IsNullOrWhiteSpace(snapshot.Game.WinnerFactionName))
    {
        await hub.Clients.Group(MatchHub.GroupName(gameId)).SendAsync("GameEnded", new
        {
            gameId,
            winnerFactionId = snapshot.Game.WinnerFactionId,
            winnerFactionName = snapshot.Game.WinnerFactionName
        });
        await hub.Clients.All.SendAsync("GamesUpdated", lobbyService.ListAvailableGames());
    }

    return Results.Ok(result);
});

app.MapGet("/api/games", (GameLobbyService service) =>
    Results.Ok(service.ListAvailableGames()));

app.MapGet("/api/maps", (GameMapService service) =>
    Results.Ok(service.ListMaps().Select(map => new { map.Id, map.Name })));

app.MapPost("/api/games", async (CreateGameRequest request, HttpRequest httpRequest, GameLobbyService service, IHubContext<MatchHub> hub) =>
{
    try
    {
        var game = service.CreateGame(request, ResolvePlayerId(httpRequest), ResolvePlayerName(httpRequest));
        await hub.Clients.All.SendAsync("GamesUpdated", service.ListAvailableGames());
        return Results.Created($"/api/games/{game.Id}", game);
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (InvalidOperationException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
});

app.MapPost("/api/games/{gameId}/join", async (string gameId, HttpRequest httpRequest, GameLobbyService service, CardiffMatchStateService stateService, IHubContext<MatchHub> hub) =>
{
    try
    {
        var result = service.JoinGame(gameId, ResolvePlayerId(httpRequest), ResolvePlayerName(httpRequest));
        // Only invalidate during the open lobby phase — invalidating a started/ended game
        // would wipe the live snapshot and re-create from scratch, losing all territory captures.
        if (result.Status == "Open")
        {
            stateService.Invalidate(gameId);
            var snapshot = stateService.GetSnapshot(gameId);
            await hub.Clients.Group(MatchHub.GroupName(gameId)).SendAsync("SnapshotUpdated", snapshot);
            await hub.Clients.All.SendAsync("GamesUpdated", service.ListAvailableGames());
        }
        else if (!string.IsNullOrWhiteSpace(ResolvePlayerName(httpRequest)))
        {
            // For started games, update the faction display name in-place without re-creating the snapshot.
            var updatedSnapshot = stateService.Update(gameId, snapshot =>
                snapshot with
                {
                    Factions = snapshot.Factions
                        .Select(f => f.Id == result.FactionId ? f with { Name = result.DisplayName } : f)
                        .ToList()
                });
            await hub.Clients.Group(MatchHub.GroupName(gameId)).SendAsync("SnapshotUpdated", updatedSnapshot);
        }
        return Results.Ok(result);
    }
    catch (InvalidOperationException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
});

app.MapPost("/api/games/{gameId}/start-position", async (string gameId, SelectStartPositionRequest request, HttpRequest httpRequest, GameLobbyService service, CardiffMatchStateService stateService, IHubContext<MatchHub> hub) =>
{
    try
    {
        var snapshot = stateService.GetSnapshot(gameId);
        var territory = snapshot.Territories.SingleOrDefault(item => item.Id == request.TerritoryId);
        if (territory is null)
        {
            return Results.BadRequest(new { error = "Territory was not found." });
        }

        if (!string.IsNullOrWhiteSpace(territory.OwnerFactionId))
        {
            return Results.BadRequest(new { error = "Starting territory must be neutral." });
        }

        var playerId = ResolvePlayerId(httpRequest);
        var factionId = service.SelectStartPosition(gameId, playerId, request.TerritoryId);
        var updatedSnapshot = stateService.ClaimStartTerritory(gameId, request.TerritoryId, factionId);
        var updatedTerritory = updatedSnapshot.Territories.SingleOrDefault(item => item.Id == request.TerritoryId);
        await BroadcastTerritoryActionResolved(hub, gameId, null, request.TerritoryId, "claim", 1, updatedTerritory?.OwnerFactionId);
        await hub.Clients.Group(MatchHub.GroupName(gameId)).SendAsync("SnapshotUpdated", updatedSnapshot);
        return Results.Ok(updatedSnapshot);
    }
    catch (InvalidOperationException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
});

app.MapPost("/api/games/{gameId}/start", async (string gameId, HttpRequest httpRequest, GameLobbyService service, CardiffMatchStateService stateService, IHubContext<MatchHub> hub) =>
{
    try
    {
        service.StartGame(gameId, ResolvePlayerId(httpRequest));
        stateService.Invalidate(gameId);
        var snapshot = stateService.GetSnapshot(gameId);
        await hub.Clients.Group(MatchHub.GroupName(gameId)).SendAsync("SnapshotUpdated", snapshot);
        await hub.Clients.All.SendAsync("GamesUpdated", service.ListAvailableGames());
        return Results.Ok(snapshot);
    }
    catch (InvalidOperationException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
});

app.MapDelete("/api/games/{gameId}", async (string gameId, GameLobbyService service, CardiffMatchStateService stateService, IHubContext<MatchHub> hub) =>
{
    if (!service.EndGame(gameId))
        return Results.NotFound(new { error = "Game was not found." });
    stateService.Update(gameId, snapshot => snapshot with
    {
        Game = snapshot.Game with { Status = "Ended", IsStarted = false, IsEnded = true }
    });
    await hub.Clients.All.SendAsync("GamesUpdated", service.ListAvailableGames());
    await hub.Clients.Group(MatchHub.GroupName(gameId)).SendAsync("GameEnded", new { gameId });
    return Results.NoContent();
});

app.MapHub<MatchHub>("/hubs/match");

app.MapFallback(context =>
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return Task.CompletedTask;
    }

    var indexPath = Path.Combine(app.Environment.WebRootPath, "index.html");
    if (!File.Exists(indexPath))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return Task.CompletedTask;
    }

    context.Response.ContentType = "text/html; charset=utf-8";
    return context.Response.SendFileAsync(indexPath);
});

app.Run();

static string ResolvePlayerId(HttpRequest request)
{
    var value = request.Headers["X-Player-Id"].FirstOrDefault();
    return string.IsNullOrWhiteSpace(value)
        ? "local-player"
        : value.Trim();
}

static string? ResolvePlayerName(HttpRequest request)
{
    var value = request.Headers["X-Player-Name"].FirstOrDefault();
    return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

static MatchSnapshot ResolveWinner(CardiffMatchStateService stateService, GameLobbyService lobbyService, string gameId)
{
    var snapshot = stateService.GetSnapshot(gameId);
    var winner = MatchVictoryEvaluator.TryGetWinner(snapshot);
    if (winner is null)
    {
        return snapshot;
    }

    lobbyService.EndGame(gameId, winner.FactionId, winner.FactionName);
    return stateService.Update(gameId, current => MatchVictoryEvaluator.ApplyVictory(current, winner));
}

static Task BroadcastTerritoryActionResolved(
    IHubContext<MatchHub> hub,
    string gameId,
    string? sourceTerritoryId,
    string targetTerritoryId,
    string actionType,
    int strength,
    string? ownerFactionId)
{
    return hub.Clients.Group(MatchHub.GroupName(gameId)).SendAsync("TerritoryActionResolved", new
    {
        gameId,
        sourceTerritoryId,
        targetTerritoryId,
        actionType,
        strength,
        ownerFactionId,
        occurredAtUtc = DateTimeOffset.UtcNow
    });
}

public partial class Program;
