using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Application;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Game.Tests.Api;

public sealed class GameApiTests
{
    private static readonly JsonSerializerOptions ApiJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task AvailableGamesEndpointReturnsEmptyListOnFirstLoad()
    {
        await using var factory = new GameWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/games");
        var games = await response.Content.ReadFromJsonAsync<IReadOnlyList<AvailableGameDto>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(games);
        Assert.Empty(games);
    }

    [Fact]
    public async Task CreateGameEndpointAddsOpenGameToAvailableGames()
    {
        await using var factory = new GameWebApplicationFactory();
        using var client = factory.CreateClient();

        var createResponse = await SendWithPlayerIdAsync(client, HttpMethod.Post, "/api/games", new CreateGameRequest(
            Name: "Evening Cardiff",
            MapArea: "Cardiff",
            MaxHumanPlayers: 4,
            NpcFactions: 6,
            TerritoryCount: 100), "player-one");
        var created = await createResponse.Content.ReadFromJsonAsync<AvailableGameDto>();
        var games = await client.GetFromJsonAsync<IReadOnlyList<AvailableGameDto>>("/api/games");

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(created);
        Assert.StartsWith("game-", created.Id);
        Assert.Equal("Evening Cardiff", created.Name);
        Assert.Equal("Open", created.Status);
        Assert.Equal(1, created.HumanPlayers);
        Assert.Equal(4, created.MaxHumanPlayers);
        Assert.Equal(6, created.NpcFactions);
        Assert.NotNull(games);
        Assert.Contains(games, game => game.Id == created.Id && game.Status == "Open" && game.HumanPlayers == 1);
    }

    [Fact]
    public async Task CreatedGameSnapshotStartsWithOnlyNpcPositionsAssigned()
    {
        await using var factory = new GameWebApplicationFactory();
        using var client = factory.CreateClient();

        var createResponse = await SendWithPlayerIdAsync(client, HttpMethod.Post, "/api/games", new CreateGameRequest(
            Name: "Four Humans",
            MapArea: "Cardiff",
            MaxHumanPlayers: 4,
            NpcFactions: 3,
            TerritoryCount: 100), "player-one");
        var created = await createResponse.Content.ReadFromJsonAsync<AvailableGameDto>();
        Assert.NotNull(created);

        var snapshot = await client.GetFromJsonAsync<MatchSnapshot>($"/api/matches/{created.Id}", ApiJsonOptions);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(snapshot);
        Assert.Equal("Open", snapshot.Game.Status);
        Assert.False(snapshot.Game.IsStarted);
        Assert.Equal(1, snapshot.Factions.Count(faction => faction.Kind == FactionKind.Human));
        Assert.Equal(3, snapshot.Factions.Count(faction => faction.Kind == FactionKind.Npc));
        Assert.Empty(snapshot.Territories.Where(territory => territory.OwnerFactionId == "human-1"));
        Assert.Equal(3, snapshot.Armies.Count);
    }

    [Fact]
    public async Task JoinEndpointAddsAnotherHumanPlayerSlot()
    {
        await using var factory = new GameWebApplicationFactory();
        using var client = factory.CreateClient();

        var createResponse = await SendWithPlayerIdAsync(client, HttpMethod.Post, "/api/games", new CreateGameRequest(
            Name: "Joinable Cardiff",
            MapArea: "Cardiff",
            MaxHumanPlayers: 3,
            NpcFactions: 2,
            TerritoryCount: 100), "player-one");
        var created = await createResponse.Content.ReadFromJsonAsync<AvailableGameDto>();
        Assert.NotNull(created);

        var joinResponse = await SendWithPlayerIdAsync(client, HttpMethod.Post, $"/api/games/{created.Id}/join", new { }, "player-two");
        var joined = await joinResponse.Content.ReadFromJsonAsync<JoinGameResponse>();
        var snapshot = await client.GetFromJsonAsync<MatchSnapshot>($"/api/matches/{created.Id}", ApiJsonOptions);

        Assert.Equal(HttpStatusCode.OK, joinResponse.StatusCode);
        Assert.NotNull(joined);
        Assert.Equal("human-2", joined.FactionId);
        Assert.NotNull(snapshot);
        Assert.Equal(2, snapshot.Game.HumanPlayers);
        Assert.Equal(2, snapshot.Factions.Count(faction => faction.Kind == FactionKind.Human));
    }

    [Fact]
    public async Task StartPositionEndpointClaimsNeutralTerritoryForJoinedPlayer()
    {
        await using var factory = new GameWebApplicationFactory();
        using var client = factory.CreateClient();

        var createResponse = await SendWithPlayerIdAsync(client, HttpMethod.Post, "/api/games", new CreateGameRequest(
            Name: "Claim Cardiff",
            MapArea: "Cardiff",
            MaxHumanPlayers: 2,
            NpcFactions: 2,
            TerritoryCount: 100), "player-one");
        var created = await createResponse.Content.ReadFromJsonAsync<AvailableGameDto>();
        Assert.NotNull(created);
        var before = await client.GetFromJsonAsync<MatchSnapshot>($"/api/matches/{created.Id}", ApiJsonOptions);
        Assert.NotNull(before);
        var neutral = before.Territories.First(territory => territory.OwnerFactionId is null);

        var startResponse = await SendWithPlayerIdAsync(client, HttpMethod.Post, $"/api/games/{created.Id}/start-position", new SelectStartPositionRequest(neutral.Id), "player-one");
        var snapshot = await startResponse.Content.ReadFromJsonAsync<MatchSnapshot>(ApiJsonOptions);

        Assert.Equal(HttpStatusCode.OK, startResponse.StatusCode);
        Assert.NotNull(snapshot);
        Assert.Equal("human-1", snapshot.Territories.Single(territory => territory.Id == neutral.Id).OwnerFactionId);
        Assert.Contains(snapshot.Armies, army => army.FactionId == "human-1" && army.TerritoryId == neutral.Id && army.Strength == 100);
    }

    [Fact]
    public async Task StartGameEndpointRequiresAllJoinedPlayersToPickStarts()
    {
        await using var factory = new GameWebApplicationFactory();
        using var client = factory.CreateClient();

        var createResponse = await SendWithPlayerIdAsync(client, HttpMethod.Post, "/api/games", new CreateGameRequest(
            Name: "Need Starts",
            MapArea: "Cardiff",
            MaxHumanPlayers: 2,
            NpcFactions: 2,
            TerritoryCount: 100), "player-one");
        var created = await createResponse.Content.ReadFromJsonAsync<AvailableGameDto>();
        Assert.NotNull(created);
        await SendWithPlayerIdAsync(client, HttpMethod.Post, $"/api/games/{created.Id}/join", new { }, "player-two");

        var startResponse = await SendWithPlayerIdAsync(client, HttpMethod.Post, $"/api/games/{created.Id}/start", new { }, "player-one");
        var problem = await startResponse.Content.ReadFromJsonAsync<Dictionary<string, string>>();

        Assert.Equal(HttpStatusCode.BadRequest, startResponse.StatusCode);
        Assert.Equal("All joined players must select a starting territory before the game can start.", problem?["error"]);
    }

    [Fact]
    public async Task StartGameEndpointLocksSelectionsAndMarksGameStarted()
    {
        await using var factory = new GameWebApplicationFactory();
        using var client = factory.CreateClient();

        var createResponse = await SendWithPlayerIdAsync(client, HttpMethod.Post, "/api/games", new CreateGameRequest(
            Name: "Start Cardiff",
            MapArea: "Cardiff",
            MaxHumanPlayers: 2,
            NpcFactions: 2,
            TerritoryCount: 100), "player-one");
        var created = await createResponse.Content.ReadFromJsonAsync<AvailableGameDto>();
        Assert.NotNull(created);
        await SendWithPlayerIdAsync(client, HttpMethod.Post, $"/api/games/{created.Id}/join", new { }, "player-two");

        var initialSnapshot = await client.GetFromJsonAsync<MatchSnapshot>($"/api/matches/{created.Id}", ApiJsonOptions);
        Assert.NotNull(initialSnapshot);
        var neutralStarts = initialSnapshot.Territories.Where(territory => territory.OwnerFactionId is null).Take(2).ToArray();

        await SendWithPlayerIdAsync(client, HttpMethod.Post, $"/api/games/{created.Id}/start-position", new SelectStartPositionRequest(neutralStarts[0].Id), "player-one");
        await SendWithPlayerIdAsync(client, HttpMethod.Post, $"/api/games/{created.Id}/start-position", new SelectStartPositionRequest(neutralStarts[1].Id), "player-two");
        var startResponse = await SendWithPlayerIdAsync(client, HttpMethod.Post, $"/api/games/{created.Id}/start", new { }, "player-one");
        var startedSnapshot = await startResponse.Content.ReadFromJsonAsync<MatchSnapshot>(ApiJsonOptions);
        var joinAfterStartResponse = await SendWithPlayerIdAsync(client, HttpMethod.Post, $"/api/games/{created.Id}/join", new { }, "player-three");

        Assert.Equal(HttpStatusCode.OK, startResponse.StatusCode);
        Assert.NotNull(startedSnapshot);
        Assert.True(startedSnapshot.Game.IsStarted);
        Assert.Equal("Started", startedSnapshot.Game.Status);
        Assert.Equal(HttpStatusCode.BadRequest, joinAfterStartResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteGameEndpointRemovesGameFromAvailableGames()
    {
        await using var factory = new GameWebApplicationFactory();
        using var client = factory.CreateClient();

        var createResponse = await SendWithPlayerIdAsync(client, HttpMethod.Post, "/api/games", new CreateGameRequest(
            Name: "Short Cardiff",
            MapArea: "Cardiff",
            MaxHumanPlayers: 2,
            NpcFactions: 4,
            TerritoryCount: 80), "player-one");
        var created = await createResponse.Content.ReadFromJsonAsync<AvailableGameDto>();

        Assert.NotNull(created);
        var deleteResponse = await client.DeleteAsync($"/api/games/{created.Id}");
        var games = await client.GetFromJsonAsync<IReadOnlyList<AvailableGameDto>>("/api/games");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.NotNull(games);
        Assert.Contains(games, game => game.Id == created.Id && game.Status == "Ended");
    }

    private static Task<HttpResponseMessage> SendWithPlayerIdAsync<T>(HttpClient client, HttpMethod method, string path, T body, string playerId)
    {
        var request = new HttpRequestMessage(method, path)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("X-Player-Id", playerId);
        return client.SendAsync(request);
    }
}
