using System.Net;
using System.Net.Http.Json;
using Game.Application;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Game.Tests.Api;

public sealed class GameApiTests
{
    [Fact]
    public async Task AvailableGamesEndpointReturnsOpenCardiffMatch()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/games");
        var games = await response.Content.ReadFromJsonAsync<IReadOnlyList<AvailableGameDto>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(games);
        var game = Assert.Single(games);
        Assert.Equal("cardiff-match", game.Id);
        Assert.Equal("Cardiff Match", game.Name);
        Assert.Equal("Open", game.Status);
        Assert.Equal("Cardiff", game.MapArea);
        Assert.Equal(1, game.HumanPlayers);
        Assert.Equal(2, game.MaxHumanPlayers);
    }

    [Fact]
    public async Task CreateGameEndpointAddsOpenGameToAvailableGames()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/games", new CreateGameRequest(
            Name: "Evening Cardiff",
            MapArea: "Cardiff",
            MaxHumanPlayers: 4,
            NpcFactions: 6,
            TerritoryCount: 100));
        var created = await createResponse.Content.ReadFromJsonAsync<AvailableGameDto>();
        var games = await client.GetFromJsonAsync<IReadOnlyList<AvailableGameDto>>("/api/games");

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(created);
        Assert.StartsWith("game-", created.Id);
        Assert.Equal("Evening Cardiff", created.Name);
        Assert.NotNull(games);
        Assert.Contains(games, game => game.Id == created.Id && game.Status == "Open");
    }

    [Fact]
    public async Task DeleteGameEndpointRemovesGameFromAvailableGames()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/games", new CreateGameRequest(
            Name: "Short Cardiff",
            MapArea: "Cardiff",
            MaxHumanPlayers: 2,
            NpcFactions: 4,
            TerritoryCount: 80));
        var created = await createResponse.Content.ReadFromJsonAsync<AvailableGameDto>();

        Assert.NotNull(created);
        var deleteResponse = await client.DeleteAsync($"/api/games/{created.Id}");
        var games = await client.GetFromJsonAsync<IReadOnlyList<AvailableGameDto>>("/api/games");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.NotNull(games);
        Assert.DoesNotContain(games, game => game.Id == created.Id);
    }
}
