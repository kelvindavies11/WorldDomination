using Game.Application;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddSingleton<CardiffMatchService>();
builder.Services.AddSingleton<CardiffMatchStateService>();
builder.Services.AddSingleton<PlayerTerritoryCommandService>();
builder.Services.AddSingleton<GameLobbyService>();
builder.Services.AddSingleton<GameMapService>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/matches/{gameId}", (string gameId, CardiffMatchStateService service) =>
    Results.Ok(service.GetSnapshot(gameId)));

app.MapPost("/api/matches/{gameId}/movements", (string gameId, SendArmyCommand request, PlayerTerritoryCommandService service) =>
{
    var result = service.SendArmyToNeutralTerritory(request with { GameId = gameId });
    return result.Accepted
        ? Results.Ok(result)
        : Results.BadRequest(new { error = result.Error });
});

app.MapGet("/api/games", (GameLobbyService service) =>
    Results.Ok(service.ListAvailableGames()));

app.MapPost("/api/games", (CreateGameRequest request, GameLobbyService service) =>
{
    try
    {
        var game = service.CreateGame(request);
        return Results.Created($"/api/games/{game.Id}", game);
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
});

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

public partial class Program;
