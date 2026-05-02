# Cardiff Full Postcode Territories Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build Cardiff match territories from bundled Doogal full-postcode data so each street-level postcode becomes a playable territory on the Cardiff map.

**Architecture:** Keep match creation in the application layer and map rendering in the existing static frontend. Add polygon and postcode fields to the territory DTO, load Cardiff postcodes from a filtered local Doogal CSV file, create deterministic small closed polygons around each postcode centroid, and render those backend-owned polygons as MapLibre territory layers. The browser should retrieve postcode territory data from the backend match/map payload and should not own the data generation process.

**Tech Stack:** ASP.NET Core minimal API, C# application services, TextFieldParser CSV parsing, xUnit, vanilla JavaScript, MapLibre GL JS, GeoJSON rendering.

---

## Data Prerequisite

Use Doogal's postcode CSV download as the source for full postcode centroid data. The app stores a filtered Cardiff-only CSV so the backend can load territory data without downloading from Doogal at runtime.

Expected source download:

```text
https://www.doogal.co.uk/UKPostcodesCSV/?Search=CF
```

Expected bundled app artifact:

```text
src/Game.Application/Data/cardiff-postcodes.csv
```

The bundled CSV must contain active rows where `District` is `Cardiff`, with these columns:

```text
Postcode,Latitude,Longitude,District,Ward,Roads,Population,Households
```

Doogal provides centroid points rather than official postcode polygons. For gameplay v1, the backend creates a small deterministic closed square polygon around each centroid. A later map creation process can replace the CSV/centroid polygon generator with official or custom polygon geometry without changing the API consumer contract.

## File Structure

- Modify `src/Game.Application/MatchDtos.cs`: add postcode and polygon geometry fields to `MatchTerritoryDto`.
- Create `src/Game.Application/PostcodeTerritoryFeature.cs`: internal data model for loaded postcode features.
- Create `src/Game.Application/CardiffPostcodeTerritoryRepository.cs`: loads and validates the bundled Doogal CSV.
- Modify `src/Game.Application/CardiffMatchService.cs`: create territories from postcode features instead of synthetic sectors.
- Modify `src/Game.Application/Game.Application.csproj`: copy the filtered CSV data file so tests and runtime can load it.
- Modify `src/Game.Api/wwwroot/app.js`: render territory polygons as MapLibre GeoJSON layers and update selected territory on click.
- Modify `tests/Game.Tests/Application/CardiffMatchServiceTests.cs`: assert postcode polygon behavior.
- Create `tests/Game.Tests/Application/CardiffPostcodeTerritoryRepositoryTests.cs`: assert CSV parsing, coordinate validation, and closed polygon creation.

---

### Task 1: Add Territory Geometry Contract

**Files:**
- Modify: `src/Game.Application/MatchDtos.cs`
- Modify: `tests/Game.Tests/Application/CardiffMatchServiceTests.cs`

- [ ] **Step 1: Write the failing test**

Add assertions to `CreatesDeterministicCardiffMatchWithDocumentedDefaults`:

```csharp
Assert.All(match.Territories, territory =>
{
    Assert.False(string.IsNullOrWhiteSpace(territory.Postcode));
    Assert.NotEmpty(territory.BoundaryCoordinates);
    Assert.Equal(territory.BoundaryCoordinates[0], territory.BoundaryCoordinates[^1]);
});
```

- [ ] **Step 2: Run the focused test**

Run:

```powershell
dotnet test Game.sln --no-restore --filter FullyQualifiedName~CardiffMatchServiceTests.CreatesDeterministicCardiffMatchWithDocumentedDefaults
```

Expected: compile failure because `MatchTerritoryDto` does not expose `Postcode` or `BoundaryCoordinates`.

- [ ] **Step 3: Add DTO fields**

Update `MatchTerritoryDto` to:

```csharp
public sealed record MatchTerritoryDto(
    string Id,
    int Index,
    string Name,
    double AreaSquareKm,
    string? OwnerFactionId,
    TerritoryStats Stats,
    IReadOnlyList<MapCoordinateDto> BoundaryCoordinates,
    string? Postcode);
```

- [ ] **Step 4: Temporarily adapt construction**

In `CardiffMatchService.CreateTerritory`, pass an empty closed placeholder so compilation reaches the intended failing assertion:

```csharp
BoundaryCoordinates:
[
    new MapCoordinateDto(-3.1791, 51.4816),
    new MapCoordinateDto(-3.1791, 51.4816)
],
Postcode: null
```

- [ ] **Step 5: Run the focused test**

Run:

```powershell
dotnet test Game.sln --no-restore --filter FullyQualifiedName~CardiffMatchServiceTests.CreatesDeterministicCardiffMatchWithDocumentedDefaults
```

Expected: fail because `Postcode` is null.

---

### Task 2: Load Bundled Cardiff Postcode GeoJSON

**Files:**
- Create: `src/Game.Application/PostcodeTerritoryFeature.cs`
- Create: `src/Game.Application/CardiffPostcodeTerritoryRepository.cs`
- Create: `tests/Game.Tests/Application/CardiffPostcodeTerritoryRepositoryTests.cs`
- Modify: `src/Game.Application/Game.Application.csproj`

- [ ] **Step 1: Write repository tests**

Create tests that parse a tiny inline GeoJSON fixture:

```csharp
using Game.Application;

namespace Game.Tests.Application;

public sealed class CardiffPostcodeTerritoryRepositoryTests
{
    [Fact]
    public void LoadsClosedPostcodePolygonsFromGeoJson()
    {
        var geoJson = """
        {
          "type": "FeatureCollection",
          "features": [
            {
              "type": "Feature",
              "properties": { "postcode": "CF10 1AA" },
              "geometry": {
                "type": "Polygon",
                "coordinates": [[[-3.18,51.48],[-3.17,51.48],[-3.17,51.49],[-3.18,51.48]]]
              }
            }
          ]
        }
        """;

        var features = CardiffPostcodeTerritoryRepository.LoadFromGeoJson(geoJson);

        var feature = Assert.Single(features);
        Assert.Equal("CF10 1AA", feature.Postcode);
        Assert.Equal("CF10 1AA", feature.Name);
        Assert.Equal(4, feature.BoundaryCoordinates.Count);
        Assert.Equal(feature.BoundaryCoordinates[0], feature.BoundaryCoordinates[^1]);
    }

    [Fact]
    public void RejectsPolygonsThatAreNotClosed()
    {
        var geoJson = """
        {
          "type": "FeatureCollection",
          "features": [
            {
              "type": "Feature",
              "properties": { "postcode": "CF10 1AA" },
              "geometry": {
                "type": "Polygon",
                "coordinates": [[[-3.18,51.48],[-3.17,51.48],[-3.17,51.49]]]
              }
            }
          ]
        }
        """;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CardiffPostcodeTerritoryRepository.LoadFromGeoJson(geoJson));

        Assert.Contains("closed", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
```

- [ ] **Step 2: Run repository tests**

Run:

```powershell
dotnet test Game.sln --no-restore --filter FullyQualifiedName~CardiffPostcodeTerritoryRepositoryTests
```

Expected: compile failure because the repository does not exist.

- [ ] **Step 3: Add feature record**

Create `PostcodeTerritoryFeature.cs`:

```csharp
namespace Game.Application;

internal sealed record PostcodeTerritoryFeature(
    string Postcode,
    string Name,
    IReadOnlyList<MapCoordinateDto> BoundaryCoordinates);
```

- [ ] **Step 4: Add repository parser**

Create `CardiffPostcodeTerritoryRepository.cs` with `System.Text.Json` parsing for `FeatureCollection`, `Polygon`, and first-ring coordinates. Validate postcode presence, at least four coordinates, and closed first/last coordinate.

- [ ] **Step 5: Run repository tests**

Run:

```powershell
dotnet test Game.sln --no-restore --filter FullyQualifiedName~CardiffPostcodeTerritoryRepositoryTests
```

Expected: pass.

---

### Task 3: Create Cardiff Match From Postcode Features

**Files:**
- Modify: `src/Game.Application/CardiffMatchService.cs`
- Modify: `tests/Game.Tests/Application/CardiffMatchServiceTests.cs`

- [ ] **Step 1: Update match tests**

Change the Cardiff match test to assert territory count is driven by the dataset:

```csharp
Assert.True(match.Territories.Count > 100);
Assert.All(match.Territories, territory => Assert.StartsWith("CF", territory.Postcode));
Assert.Contains(match.Territories, territory => territory.Name.Contains("CF", StringComparison.OrdinalIgnoreCase));
```

- [ ] **Step 2: Run focused tests**

Run:

```powershell
dotnet test Game.sln --no-restore --filter FullyQualifiedName~CardiffMatchServiceTests
```

Expected: fail while service still creates synthetic sectors.

- [ ] **Step 3: Replace synthetic territory generation**

Inject or instantiate `CardiffPostcodeTerritoryRepository`, load `cardiff-postcode-territories.geojson`, and map each feature into `MatchTerritoryDto`.

Use deterministic ids:

```csharp
var id = $"postcode-{NormalizePostcode(feature.Postcode)}";
```

where `NormalizePostcode("CF10 1AA")` returns `cf10-1aa`.

- [ ] **Step 4: Assign starts from dataset positions**

Compute start indexes as proportional positions across the loaded territory list:

```csharp
var startIndexes = new Dictionary<string, int>
{
    ["human-1"] = 0,
    ["human-2"] = territories.Count - 1,
    ["npc-1"] = territories.Count / 7,
    ["npc-2"] = territories.Count * 2 / 7,
    ["npc-3"] = territories.Count * 3 / 7,
    ["npc-4"] = territories.Count * 4 / 7,
    ["npc-5"] = territories.Count * 5 / 7,
    ["npc-6"] = territories.Count * 6 / 7
};
```

- [ ] **Step 5: Run focused tests**

Run:

```powershell
dotnet test Game.sln --no-restore --filter FullyQualifiedName~CardiffMatchServiceTests
```

Expected: pass once the real data file exists.

---

### Task 4: Render Territory Polygons On The Map

**Files:**
- Modify: `src/Game.Api/wwwroot/app.js`

- [ ] **Step 1: Add frontend shell assertions**

Extend existing API/static shell tests to check that the shell script contains territory layer hooks:

```csharp
Assert.Contains("territory-fill", html);
Assert.Contains("data-selected-postcode", html);
```

- [ ] **Step 2: Run the focused test**

Run:

```powershell
dotnet test Game.sln --no-restore --filter FullyQualifiedName~MatchApiTests
```

Expected: fail until app.js includes the hooks.

- [ ] **Step 3: Add selected postcode markup**

In the selected territory widget, add:

```html
<p class="muted" data-selected-postcode></p>
```

- [ ] **Step 4: Add MapLibre territory source and layers**

After `addPlayAreaBoundary(activeMap)`, call:

```javascript
addTerritoryLayers(activeMap);
```

Create `territoryFeatureCollection()` from `state.matchSnapshot.territories`, mapping `boundaryCoordinates` to GeoJSON polygon coordinates and including `id`, `name`, `postcode`, `ownerFactionId`, and faction color in properties.

Add layers:

```javascript
territory-fill
territory-outline
territory-selected-outline
```

- [ ] **Step 5: Add click selection**

On `territory-fill` click, store the selected territory id in state and call `updateMatchDataInPlace()`. Make `selectedTerritory()` return the selected id when available.

- [ ] **Step 6: Run focused tests**

Run:

```powershell
dotnet test Game.sln --no-restore --filter FullyQualifiedName~MatchApiTests
```

Expected: pass.

---

### Task 5: Verification

**Files:**
- No additional files.

- [ ] **Step 1: Stop old API process**

Run:

```powershell
Get-Process Game.Api -ErrorAction SilentlyContinue | Stop-Process
```

- [ ] **Step 2: Run full tests**

Run:

```powershell
dotnet test Game.sln --no-restore
```

Expected: all tests pass.

- [ ] **Step 3: Start API**

Run:

```powershell
dotnet run --project src/Game.Api/Game.Api.csproj --urls http://localhost:5057
```

- [ ] **Step 4: Verify match route**

Request:

```powershell
Invoke-WebRequest http://localhost:5057/games/cardiff
```

Expected: `200 OK` and HTML containing `match-map`.

---

## Self-Review

- Spec coverage: The plan covers full-postcode polygons, bundled app data, backend DTO changes, Cardiff match generation, frontend rendering, click selection, and tests.
- Placeholder scan: No implementation step relies on vague placeholders. The only prerequisite is the official OS source data file, which is explicit because the full Cardiff polygon dataset is external product data.
- Type consistency: `BoundaryCoordinates`, `Postcode`, `CardiffPostcodeTerritoryRepository`, and `PostcodeTerritoryFeature` are named consistently across tasks.
