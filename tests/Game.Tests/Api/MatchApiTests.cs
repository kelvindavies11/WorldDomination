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
    public void MovementEndpointBroadcastsTacticalPulsePresentationEvent()
    {
        var programSource = File.ReadAllText(FindProjectFile("src", "Game.Api", "Program.cs"));

        Assert.Contains("TerritoryActionResolved", programSource);
        Assert.Contains("\"attack\"", programSource);
        Assert.Contains("\"reinforce\"", programSource);
        Assert.Contains("\"claim\"", programSource);
        Assert.Contains("request.Strength", programSource);
    }

    private static string FindProjectFile(params string[] pathSegments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(pathSegments).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find {Path.Combine(pathSegments)}.");
    }

    [Fact]
    public async Task CardiffMatchEndpointReturnsMatchSnapshot()
    {
        await using var factory = new GameWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/matches/cardiff");
        var snapshot = await response.Content.ReadFromJsonAsync<MatchSnapshot>(ApiJsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(snapshot);
        Assert.Equal("cardiff-match", snapshot.GameId);
        Assert.Equal("Cardiff & Newport", snapshot.MapArea);
        Assert.True(snapshot.SnapshotGeneratedAtUtc > DateTimeOffset.MinValue);
        Assert.True(snapshot.Game.IsStarted);
        Assert.Equal("Started", snapshot.Game.Status);
        Assert.Equal("Cardiff & Newport", snapshot.Map.Name);
        Assert.Equal(18, snapshot.Map.BoundaryCoordinates.Count);
        Assert.Equal(snapshot.Map.BoundaryCoordinates[0], snapshot.Map.BoundaryCoordinates[^1]);
        Assert.True(snapshot.Territories.Count >= 76);
        Assert.Contains(snapshot.Territories, territory => territory.Postcode == "CF64 1");
        Assert.Contains(snapshot.Territories, territory => territory.Postcode == "CF64 4");
        Assert.Contains(snapshot.Territories, territory => territory.Postcode == "CF5 6");
        Assert.Contains(snapshot.Territories, territory => territory.Postcode == "NP20 1");
        Assert.All(snapshot.Territories, territory =>
        {
            Assert.False(string.IsNullOrWhiteSpace(territory.Postcode));
            Assert.NotNull(territory.Stats);
            Assert.NotEmpty(territory.BoundaryCoordinates);
        });
        Assert.Equal(8, snapshot.Leaderboard.Count);
        Assert.All(snapshot.Leaderboard, row =>
        {
            Assert.True(row.TerritoryCount >= 0);
            Assert.True(row.Revenue >= 0);
            Assert.True(row.ArmyStrength >= 0);
            Assert.True(row.ArmyGrowth >= 0);
        });
    }

    [Fact]
    public async Task CardiffMatchEndpointKeepsTerritoriesAtOneLevel()
    {
        await using var factory = new GameWebApplicationFactory();
        using var client = factory.CreateClient();

        using var document = JsonDocument.Parse(await client.GetStringAsync("/api/matches/cardiff"));
        var root = document.RootElement;
        var map = root.GetProperty("map");
        var territory = root.GetProperty("territories")[0];

        Assert.False(map.TryGetProperty("territories", out _));
        Assert.True(territory.TryGetProperty("boundaryCoordinates", out _));
        Assert.True(territory.TryGetProperty("ownerFactionId", out _));
        Assert.True(territory.TryGetProperty("stats", out _));
        Assert.True(root.GetProperty("game").TryGetProperty("status", out _));
        Assert.True(root.GetProperty("game").TryGetProperty("isStarted", out _));
        Assert.True(root.GetProperty("leaderboard")[0].TryGetProperty("revenue", out _));
        Assert.True(root.GetProperty("leaderboard")[0].TryGetProperty("armyGrowth", out _));
        Assert.True(root.GetProperty("leaderboard")[0].TryGetProperty("armyStrength", out _));
        Assert.True(root.GetProperty("leaderboard")[0].TryGetProperty("territoryCount", out _));
    }

    [Fact]
    public async Task CardiffMatchEndpointReturnsStringFactionKindsForBrowserClients()
    {
        await using var factory = new GameWebApplicationFactory();
        using var client = factory.CreateClient();

        var json = await client.GetStringAsync("/api/matches/cardiff");

        Assert.Contains("\"kind\":\"Human\"", json);
        Assert.Contains("\"kind\":\"Npc\"", json);
    }

    [Fact]
    public async Task CardiffMovementEndpointCapturesConnectedNeutralTerritory()
    {
        await using var factory = new GameWebApplicationFactory();
        using var client = factory.CreateClient();
        var before = await client.GetFromJsonAsync<MatchSnapshot>("/api/matches/cardiff", ApiJsonOptions);
        Assert.NotNull(before);
        var controlBefore = before.Leaderboard.Single(row => row.FactionId == "human-1").MapControlPercentage;
        var source = before.Territories.Single(territory => territory.OwnerFactionId == "human-1");
        var route = before.Routes.First(route =>
        {
            var targetTerritoryId = route.SourceTerritoryId == source.Id
                ? route.DestinationTerritoryId
                : route.DestinationTerritoryId == source.Id
                    ? route.SourceTerritoryId
                    : null;

            return targetTerritoryId is not null &&
                before.Territories.Single(territory => territory.Id == targetTerritoryId).OwnerFactionId is null;
        });
        var targetTerritoryId = route.SourceTerritoryId == source.Id
            ? route.DestinationTerritoryId
            : route.SourceTerritoryId;

        var response = await client.PostAsJsonAsync("/api/matches/cardiff/movements", new
        {
            playerFactionId = "human-1",
            sourceTerritoryId = source.Id,
            targetTerritoryId,
            strength = 40
        }, ApiJsonOptions);
        var result = await response.Content.ReadFromJsonAsync<SendArmyResult>(ApiJsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.True(result.Accepted);
        Assert.NotNull(result.Snapshot);
        var standing = result.Snapshot.Leaderboard.Single(row => row.FactionId == "human-1");
        Assert.Equal("Player 1", standing.FactionName);
        Assert.True(standing.MapControlPercentage > controlBefore);
        Assert.True(standing.TerritoryCount >= 2);
        Assert.True(standing.Revenue > 0);
        Assert.True(standing.ArmyStrength >= 100);
        Assert.True(standing.ArmyGrowth >= standing.Revenue);
        Assert.Equal("human-1", result.Snapshot.Territories.Single(territory => territory.Id == targetTerritoryId).OwnerFactionId);
        Assert.Equal(60, result.Snapshot.Armies.Single(army => army.TerritoryId == source.Id && army.FactionId == "human-1").Strength);
        Assert.Equal(40, result.Snapshot.Armies.Single(army => army.TerritoryId == targetTerritoryId && army.FactionId == "human-1").Strength);
    }

    [Fact]
    public async Task CreatedGameSnapshotDoesNotInheritCapturedTerritoriesFromOtherGames()
    {
        await using var factory = new GameWebApplicationFactory();
        using var client = factory.CreateClient();
        var cardiffBefore = await client.GetFromJsonAsync<MatchSnapshot>("/api/matches/cardiff", ApiJsonOptions);
        Assert.NotNull(cardiffBefore);
        var source = cardiffBefore.Territories.First(territory => territory.OwnerFactionId == "human-1");
        var route = cardiffBefore.Routes.First(route =>
        {
            var targetTerritoryId = route.SourceTerritoryId == source.Id
                ? route.DestinationTerritoryId
                : route.DestinationTerritoryId == source.Id
                    ? route.SourceTerritoryId
                    : null;

            return targetTerritoryId is not null &&
                cardiffBefore.Territories.Single(territory => territory.Id == targetTerritoryId).OwnerFactionId is null;
        });
        var targetTerritoryId = route.SourceTerritoryId == source.Id
            ? route.DestinationTerritoryId
            : route.SourceTerritoryId;

        var captureResponse = await client.PostAsJsonAsync("/api/matches/cardiff/movements", new
        {
            playerFactionId = "human-1",
            sourceTerritoryId = source.Id,
            targetTerritoryId,
            strength = 40
        }, ApiJsonOptions);
        var createResponse = await client.PostAsJsonAsync("/api/games", new CreateGameRequest(
            Name: "Fresh Cardiff",
            MapArea: "Cardiff",
            MaxHumanPlayers: 2,
            NpcFactions: 6,
            TerritoryCount: 100), ApiJsonOptions);
        var created = await createResponse.Content.ReadFromJsonAsync<AvailableGameDto>(ApiJsonOptions);
        Assert.NotNull(created);
        var createdSnapshot = await client.GetFromJsonAsync<MatchSnapshot>($"/api/matches/{created.Id}", ApiJsonOptions);

        Assert.Equal(HttpStatusCode.OK, captureResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(createdSnapshot);
        Assert.Equal(created.Id, createdSnapshot.GameId);
        Assert.Equal("Open", createdSnapshot.Game.Status);
        Assert.False(createdSnapshot.Game.IsStarted);
        Assert.Empty(createdSnapshot.Territories.Where(territory => territory.OwnerFactionId is "human-1" or "human-2"));
    }

    [Fact]
    public async Task CardiffMovementEndpointReturnsBadRequestForInvalidCommand()
    {
        await using var factory = new GameWebApplicationFactory();
        using var client = factory.CreateClient();
        var before = await client.GetFromJsonAsync<MatchSnapshot>("/api/matches/cardiff", ApiJsonOptions);
        Assert.NotNull(before);
        var source = before.Territories.First(territory => territory.OwnerFactionId == "human-1");
        var route = before.Routes.First(route =>
            route.SourceTerritoryId == source.Id ||
            route.DestinationTerritoryId == source.Id);
        var targetTerritoryId = route.SourceTerritoryId == source.Id
            ? route.DestinationTerritoryId
            : route.SourceTerritoryId;

        var response = await client.PostAsJsonAsync("/api/matches/cardiff/movements", new
        {
            playerFactionId = "human-1",
            sourceTerritoryId = source.Id,
            targetTerritoryId,
            strength = 0
        }, ApiJsonOptions);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Strength must be at least 1.", document.RootElement.GetProperty("error").GetString());
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
        Assert.DoesNotContain("floating-widget selected-territory-widget", appScript);
        Assert.Contains("command-bar", appScript);
        Assert.Contains("floating-widget leaderboard-widget", appScript);
        Assert.Contains("data-action=\"toggle-widget\"", appScript);
        Assert.Contains("data-selected-name", appScript);
        Assert.Contains("data-selected-owner", appScript);
        Assert.Contains("data-match-generated-at", appScript);
        Assert.Contains("territory-fill", appScript);
        Assert.Contains("territory-hover-fill", appScript);
        Assert.Contains("hoveredTerritoryId", appScript);
        Assert.Contains("matchSnapshot?.territories", appScript);
        Assert.DoesNotContain("map?.territories", appScript);
        Assert.Contains("territoryFillPaint()", appScript);
        Assert.Contains("ownerColorForTerritory", appScript);
        Assert.Contains("ownerColor", appScript);
        Assert.Contains("data-widget-body", appScript);
        Assert.Contains("collapsedWidgets", appScript);
        Assert.Contains("updateWidgetCollapseState", appScript);
        Assert.Contains("updateWidgetCollapseState(widget);", appScript[appScript.IndexOf("function toggleWidget", StringComparison.Ordinal)..appScript.IndexOf("function updateWidgetCollapseState", StringComparison.Ordinal)]);
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
    public void CardiffMatchFrontendAssetDeclaresTerritoryControlHooks()
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

        Assert.Contains("selectedSourceTerritoryId", appScript);
        Assert.Contains("selectedTargetTerritoryId", appScript);
        Assert.Contains("data-action=\"send-movement\"", appScript);
        Assert.Contains("armyStrengthSlider", appScript);
        Assert.Contains("matchApiPath", appScript);
        Assert.Contains("currentGameId", appScript);
        Assert.Contains("`/api/matches/${encodeURIComponent(currentGameId())}`", appScript);
        Assert.Contains("`${matchApiPath()}/movements`", appScript);
        Assert.DoesNotContain("/api/matches/cardiff/movements", appScript);
        Assert.Contains("territory-valid-target-outline", appScript);
        Assert.Contains("validTargetTerritoryIds", appScript);
        Assert.DoesNotContain("validTargetFilter", appScript);
        Assert.DoesNotContain("isValidExpansionTarget", appScript);
        Assert.DoesNotContain("[\"==\", [\"get\", \"isValidExpansionTarget\"], true]", appScript);
        Assert.Contains("data-valid-targets", appScript);
        Assert.Contains("data-movement-panel", appScript);
        Assert.Contains("selectedTerritoryOutlineColorPaint", appScript);
        Assert.Contains("selectedTerritoryOutlineColorPaint", appScript);
        Assert.Contains("selectedExpansionTargetColor", appScript);
        Assert.Contains("[\"coalesce\", [\"get\", \"ownerColor\"], \"#ffffff\"]", appScript);
        Assert.Contains("selectedTargetTerritoryId", appScript);
        Assert.Contains("territory-valid-target-shadow", appScript);
        Assert.Contains("territory-target-selection-outline", appScript);
        Assert.Contains("territory-capture-expansion", appScript);
        Assert.DoesNotContain("territory-attack-animation", appScript);
        Assert.DoesNotContain("territory-attack-trail", appScript);
        Assert.DoesNotContain("territory-attack-impact", appScript);
        Assert.Contains("captureExpansionFillPaint", appScript);
        Assert.DoesNotContain("attackTrailPaint", appScript);
        Assert.DoesNotContain("attackImpactPaint", appScript);
        Assert.DoesNotContain("animateTerritoryTakeover", appScript);
        Assert.DoesNotContain("animateTerritoryAttack", appScript);
        Assert.Contains("animateTerritoryCaptureExpansion", appScript);
        Assert.Contains("captureExpansionFeature", appScript);
        Assert.DoesNotContain("attackAnimationFeatureCollection", appScript);
        Assert.Contains("requestAnimationFrame", appScript);
        Assert.Contains("type: \"Polygon\"", appScript);
        Assert.Contains("type: \"circle\"", appScript);
        Assert.Contains("preserveExpansionSelection", appScript);
        Assert.Contains("\"line-color\": \"#07130f\"", appScript);
        Assert.Contains("\"line-color\": \"#7cffd4\"", appScript);
        Assert.Contains("\"line-dasharray\": [1.2, 0.8]", appScript);
        Assert.DoesNotContain("territory-valid-target-fill", appScript);
        Assert.DoesNotContain("fill-pattern", appScript);
    }

    [Fact]
    public void CardiffMatchFrontendAssetShowsEveryLeaderboardRowAndLabelsCurrentPlayer()
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

        Assert.Contains("leaderboardDisplayName", appScript);
        Assert.Contains("leaderboardControlText", appScript);
        Assert.Contains("`${name} (You)`", appScript);
        Assert.Contains("rows?.length ? rows : fallbackRows).map", appScript);
        Assert.Contains("toFixed(1)", appScript);
        Assert.DoesNotContain("Math.round(row.mapControlPercentage)", appScript);
        Assert.DoesNotContain("slice(0, 6).map", appScript);
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
        return new GameWebApplicationFactory()
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
