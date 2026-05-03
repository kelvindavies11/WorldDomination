using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Application;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Game.Tests.Api;

public sealed class MatchApiTests
{
    private static readonly JsonSerializerOptions ApiJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task CardiffMatchEndpointReturnsMatchSnapshot()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/matches/cardiff");
        var snapshot = await response.Content.ReadFromJsonAsync<MatchSnapshot>(ApiJsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(snapshot);
        Assert.Equal("cardiff-match", snapshot.GameId);
        Assert.Equal("Cardiff", snapshot.MapArea);
        Assert.True(snapshot.SnapshotGeneratedAtUtc > DateTimeOffset.MinValue);
        Assert.Equal("Cardiff", snapshot.Map.Name);
        Assert.Equal(15, snapshot.Map.BoundaryCoordinates.Count);
        Assert.Equal(snapshot.Map.BoundaryCoordinates[0], snapshot.Map.BoundaryCoordinates[^1]);
        Assert.True(snapshot.Territories.Count >= 58);
        Assert.Contains(snapshot.Territories, territory => territory.Postcode == "CF64 1");
        Assert.Contains(snapshot.Territories, territory => territory.Postcode == "CF64 4");
        Assert.Contains(snapshot.Territories, territory => territory.Postcode == "CF5 6");
        Assert.All(snapshot.Territories, territory =>
        {
            Assert.False(string.IsNullOrWhiteSpace(territory.Postcode));
            Assert.NotNull(territory.Stats);
            Assert.NotEmpty(territory.BoundaryCoordinates);
        });
        Assert.Equal(8, snapshot.Leaderboard.Count);
    }

    [Fact]
    public async Task CardiffMatchEndpointKeepsTerritoriesAtOneLevel()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var document = JsonDocument.Parse(await client.GetStringAsync("/api/matches/cardiff"));
        var root = document.RootElement;
        var map = root.GetProperty("map");
        var territory = root.GetProperty("territories")[0];

        Assert.False(map.TryGetProperty("territories", out _));
        Assert.True(territory.TryGetProperty("boundaryCoordinates", out _));
        Assert.True(territory.TryGetProperty("ownerFactionId", out _));
        Assert.True(territory.TryGetProperty("stats", out _));
    }

    [Fact]
    public async Task CardiffMatchEndpointReturnsStringFactionKindsForBrowserClients()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var json = await client.GetStringAsync("/api/matches/cardiff");

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
    public void CardiffMatchFrontendAssetDeclaresMapLayoutHooks()
    {
        var appScript = File.ReadAllText(Path.Combine(
            Directory.GetCurrentDirectory(),
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "Game.Api",
            "wwwroot",
            "app.js"));

        Assert.Contains("match-map-shell", appScript);
        Assert.Contains("match-map", appScript);
        Assert.Contains("floating-widget selected-territory-widget", appScript);
        Assert.Contains("floating-widget leaderboard-widget", appScript);
        Assert.Contains("data-action=\"toggle-widget\"", appScript);
        Assert.Contains("data-selected-postcode", appScript);
        Assert.Contains("data-match-generated-at", appScript);
        Assert.Contains("territory-fill", appScript);
        Assert.Contains("territory-hover-fill", appScript);
        Assert.Contains("hoveredTerritoryId", appScript);
        Assert.Contains("matchSnapshot?.territories", appScript);
        Assert.DoesNotContain("map?.territories", appScript);
        Assert.Contains("\"fill-opacity\": 0.22", appScript);
        Assert.Contains("data-widget-body", appScript);
        Assert.Contains("collapsedWidgets", appScript);
        Assert.Contains("updateWidgetCollapseState", appScript);
        Assert.DoesNotContain("render();\r\n}", appScript[appScript.IndexOf("function toggleWidget", StringComparison.Ordinal)..appScript.IndexOf("app.addEventListener", StringComparison.Ordinal)]);
        Assert.DoesNotContain("match-command-strip", appScript);
        Assert.DoesNotContain("match-header", appScript);
    }

    [Fact]
    public void CardiffMatchFrontendAssetDeclaresPlayableAreaBoundary()
    {
        var appScript = File.ReadAllText(Path.Combine(
            Directory.GetCurrentDirectory(),
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "Game.Api",
            "wwwroot",
            "app.js"));

        Assert.Contains("currentMapDetails", appScript);
        Assert.Contains("boundaryCoordinates", appScript);
        Assert.Contains("cameraBounds", appScript);
        Assert.Contains("maxBounds", appScript);
        Assert.Contains("out-of-bounds-mask", appScript);
        Assert.Contains("play-area-fill", appScript);
        Assert.Contains("play-area-outline", appScript);
        Assert.DoesNotContain("const MAP_DETAILS", appScript);
        Assert.DoesNotContain("function playAreaBoundaryFeature(bounds)", appScript);
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
