using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Application;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Game.Tests.Api;

public sealed class PrototypeApiTests
{
    private static readonly JsonSerializerOptions ApiJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task CardiffPrototypeEndpointReturnsMatchSnapshot()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/prototype/cardiff");
        var snapshot = await response.Content.ReadFromJsonAsync<PrototypeMatchSnapshot>(ApiJsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(snapshot);
        Assert.Equal("cardiff-prototype", snapshot.GameId);
        Assert.Equal("Cardiff", snapshot.MapArea);
        Assert.Equal(100, snapshot.Territories.Count);
        Assert.Equal(8, snapshot.Leaderboard.Count);
    }

    [Fact]
    public async Task CardiffPrototypeEndpointReturnsStringFactionKindsForBrowserClients()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var json = await client.GetStringAsync("/api/prototype/cardiff");

        Assert.Contains("\"kind\":\"Human\"", json);
        Assert.Contains("\"kind\":\"Npc\"", json);
    }

    [Fact]
    public async Task RootReturnsFrontendShellWhenStaticIndexExists()
    {
        using var staticSite = StaticSiteFixture.Create();
        await using var factory = CreateFactoryWithWebRoot(staticSite.Root);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Game Web Shell", html);
    }

    [Fact]
    public async Task ClientRouteReturnsFrontendShellWhenStaticIndexExists()
    {
        using var staticSite = StaticSiteFixture.Create();
        await using var factory = CreateFactoryWithWebRoot(staticSite.Root);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/games/cardiff/lobby");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Game Web Shell", html);
    }

    [Fact]
    public async Task UnknownApiRouteDoesNotReturnFrontendShell()
    {
        using var staticSite = StaticSiteFixture.Create();
        await using var factory = CreateFactoryWithWebRoot(staticSite.Root);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/missing");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("Game Web Shell", html);
    }

    private static WebApplicationFactory<Program> CreateFactoryWithWebRoot(string webRoot)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting(WebHostDefaults.WebRootKey, webRoot);
            });
    }

    private sealed class StaticSiteFixture : IDisposable
    {
        private StaticSiteFixture(string root) => Root = root;

        public string Root { get; }

        public static StaticSiteFixture Create()
        {
            var root = Path.Combine(Path.GetTempPath(), $"game-web-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "index.html"), "<!doctype html><title>Game Web Shell</title>");
            return new StaticSiteFixture(root);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
