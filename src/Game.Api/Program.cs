using Game.Application;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddSingleton<CardiffMatchService>();
builder.Services.AddSingleton<GameLobbyService>();
builder.Services.AddSingleton<GameMapService>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/matches/cardiff", (CardiffMatchService service) =>
    Results.Ok(service.CreateCardiffMatch()));

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
