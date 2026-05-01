using System.Net;
using System.Net.Http.Json;
using Game.Application;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Game.Tests.Api;

public sealed class PrototypeApiTests
{
    [Fact]
    public async Task CardiffPrototypeEndpointReturnsMatchSnapshot()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/prototype/cardiff");
        var snapshot = await response.Content.ReadFromJsonAsync<PrototypeMatchSnapshot>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(snapshot);
        Assert.Equal("cardiff-prototype", snapshot.GameId);
        Assert.Equal("Cardiff", snapshot.MapArea);
        Assert.Equal(100, snapshot.Territories.Count);
        Assert.Equal(8, snapshot.Leaderboard.Count);
    }
}
