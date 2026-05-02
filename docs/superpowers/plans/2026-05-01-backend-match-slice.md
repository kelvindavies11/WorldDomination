# Backend Match Slice Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first tested backend match for the Dynamic OSM World Domination game.

**Architecture:** Create a .NET 10 clean architecture solution with pure domain rules in `Game.Domain`, use-case orchestration in `Game.Application`, a thin Minimal API in `Game.Api`, and automated tests in `Game.Tests`. This first slice uses deterministic in-memory/generated Cardiff-style data so the core loop can be exercised before OSM ingestion, MySQL, SignalR, and the React client are added.

**Tech Stack:** .NET 10, ASP.NET Core Minimal APIs, xUnit, C# records/classes, no database dependency in this slice.

---

## File Structure

- `Game.sln`: solution file.
- `src/Game.Domain/Game.Domain.csproj`: pure game rules project.
- `src/Game.Domain/Ruleset.cs`: versioned constants and formula weights.
- `src/Game.Domain/TerritoryFeatureSummary.cs`: OSM-derived feature summary input model.
- `src/Game.Domain/TerritoryStats.cs`: Economy, Defense, Mobility, and Strategic Value output model.
- `src/Game.Domain/TerritoryStatCalculator.cs`: pure stat formulas from `docs/game-rules.md`.
- `src/Game.Domain/MovementRoute.cs`: route type and movement route inputs.
- `src/Game.Domain/MovementCalculator.cs`: ETA and blocked-route behavior.
- `src/Game.Domain/CombatCalculator.cs`: battle power and deterministic combat result behavior.
- `src/Game.Domain/MapControlCalculator.cs`: area-based leaderboard and victory calculations.
- `src/Game.Application/Game.Application.csproj`: match orchestration project.
- `src/Game.Application/MatchMatchService.cs`: creates a deterministic Cardiff match match snapshot.
- `src/Game.Application/MatchDtos.cs`: API-facing DTOs for match state.
- `src/Game.Api/Game.Api.csproj`: Minimal API host.
- `src/Game.Api/Program.cs`: maps match endpoints.
- `tests/Game.Tests/Game.Tests.csproj`: xUnit tests.
- `tests/Game.Tests/Domain/*.cs`: domain formula tests.
- `tests/Game.Tests/Application/*.cs`: match match tests.
- `tests/Game.Tests/Api/*.cs`: API endpoint tests.

## Task 1: Solution Skeleton

**Files:**
- Create: `Game.sln`
- Create: `src/Game.Domain/Game.Domain.csproj`
- Create: `src/Game.Application/Game.Application.csproj`
- Create: `src/Game.Api/Game.Api.csproj`
- Create: `tests/Game.Tests/Game.Tests.csproj`

- [ ] **Step 1: Scaffold the solution and projects**

Run:

```powershell
dotnet new sln -n Game
dotnet new classlib -n Game.Domain -o src/Game.Domain --framework net10.0
dotnet new classlib -n Game.Application -o src/Game.Application --framework net10.0
dotnet new web -n Game.Api -o src/Game.Api --framework net10.0
dotnet new xunit -n Game.Tests -o tests/Game.Tests --framework net10.0
dotnet sln Game.sln add src/Game.Domain/Game.Domain.csproj src/Game.Application/Game.Application.csproj src/Game.Api/Game.Api.csproj tests/Game.Tests/Game.Tests.csproj
dotnet add src/Game.Application/Game.Application.csproj reference src/Game.Domain/Game.Domain.csproj
dotnet add src/Game.Api/Game.Api.csproj reference src/Game.Application/Game.Application.csproj
dotnet add tests/Game.Tests/Game.Tests.csproj reference src/Game.Domain/Game.Domain.csproj src/Game.Application/Game.Application.csproj src/Game.Api/Game.Api.csproj
dotnet add tests/Game.Tests/Game.Tests.csproj package Microsoft.AspNetCore.Mvc.Testing
```

Expected: all projects are created and references follow `Game.Api -> Game.Application -> Game.Domain`.

- [ ] **Step 2: Run the generated tests**

Run:

```powershell
dotnet test Game.sln
```

Expected: generated xUnit test passes.

## Task 2: Territory Stat Formulas

**Files:**
- Create: `tests/Game.Tests/Domain/TerritoryStatCalculatorTests.cs`
- Create: `src/Game.Domain/Ruleset.cs`
- Create: `src/Game.Domain/TerritoryFeatureSummary.cs`
- Create: `src/Game.Domain/TerritoryStats.cs`
- Create: `src/Game.Domain/TerritoryStatCalculator.cs`

- [ ] **Step 1: Write failing tests**

Test Cardiff-style mixed features, capped scores, and strategic value composition:

```csharp
using Game.Domain;

namespace Game.Tests.Domain;

public sealed class TerritoryStatCalculatorTests
{
    [Fact]
    public void CalculatesEconomyDefenseMobilityAndStrategicValueFromFeatureScores()
    {
        var features = new TerritoryFeatureSummary(
            Factories: 5,
            Shops: 8,
            CommercialAreas: 3,
            Offices: 4,
            IndustrialSites: 2,
            FarmlandOrResources: 1,
            PopulationSupport: 6,
            Mountains: 0,
            Hills: 3,
            MilitarySites: 1,
            CastlesOrForts: 1,
            GovernmentSites: 2,
            Chokepoints: 4,
            UrbanDensity: 7,
            Roads: 9,
            Railways: 3,
            BridgesOrTunnels: 4,
            Airports: 0,
            Ports: 1,
            Connections: 6,
            AreaSquareKm: 1.5,
            SpecialFeatures: 2);

        var stats = TerritoryStatCalculator.Calculate(features, Ruleset.Default);

        Assert.Equal(55, stats.Economy);
        Assert.Equal(38, stats.Defense);
        Assert.Equal(57, stats.Mobility);
        Assert.Equal(49, stats.StrategicValue);
    }

    [Fact]
    public void CapsDenseFeatureCountsAtOneHundred()
    {
        var features = TerritoryFeatureSummary.Empty with
        {
            Factories = 999,
            Shops = 999,
            CommercialAreas = 999,
            Offices = 999,
            IndustrialSites = 999,
            FarmlandOrResources = 999,
            PopulationSupport = 999
        };

        var stats = TerritoryStatCalculator.Calculate(features, Ruleset.Default);

        Assert.Equal(100, stats.Economy);
    }
}
```

- [ ] **Step 2: Run red**

Run:

```powershell
dotnet test tests/Game.Tests/Game.Tests.csproj --filter TerritoryStatCalculatorTests
```

Expected: fails because the domain types do not exist.

- [ ] **Step 3: Implement minimal formula types**

Create records and calculator using the formulas in `docs/game-rules.md`, with integer scores rounded using `MidpointRounding.AwayFromZero`.

- [ ] **Step 4: Run green**

Run:

```powershell
dotnet test tests/Game.Tests/Game.Tests.csproj --filter TerritoryStatCalculatorTests
```

Expected: both tests pass.

## Task 3: Movement ETA And Blocking

**Files:**
- Create: `tests/Game.Tests/Domain/MovementCalculatorTests.cs`
- Create: `src/Game.Domain/MovementRoute.cs`
- Create: `src/Game.Domain/MovementCalculator.cs`

- [ ] **Step 1: Write failing tests**

Cover road/rail speed, hill/mountain slowdowns, bridge/tunnel crossings, and invalid water crossings.

- [ ] **Step 2: Run red**

Run:

```powershell
dotnet test tests/Game.Tests/Game.Tests.csproj --filter MovementCalculatorTests
```

Expected: fails because movement types do not exist.

- [ ] **Step 3: Implement minimal movement calculation**

Use `ETA seconds = base_distance_seconds * terrain_multiplier * barrier_multiplier * transport_multiplier`. Return a blocked result for invalid water crossings.

- [ ] **Step 4: Run green**

Run:

```powershell
dotnet test tests/Game.Tests/Game.Tests.csproj --filter MovementCalculatorTests
```

Expected: movement tests pass.

## Task 4: Combat, Leaderboard, Elimination, Victory

**Files:**
- Create: `tests/Game.Tests/Domain/CombatCalculatorTests.cs`
- Create: `tests/Game.Tests/Domain/MapControlCalculatorTests.cs`
- Create: `src/Game.Domain/CombatCalculator.cs`
- Create: `src/Game.Domain/MapControlCalculator.cs`

- [ ] **Step 1: Write failing combat tests**

Cover territory defense bonus, attack/defense position modifiers, and survivor scaling for a deterministic battle.

- [ ] **Step 2: Run combat red**

Run:

```powershell
dotnet test tests/Game.Tests/Game.Tests.csproj --filter CombatCalculatorTests
```

Expected: fails because combat types do not exist.

- [ ] **Step 3: Implement minimal combat calculation**

Implement effective attacker and defender strength exactly from `docs/game-rules.md`; omit random battle factor in this deterministic match slice.

- [ ] **Step 4: Write failing map-control tests**

Cover map-control percentage by area, elimination status when a faction controls zero area, elimination credit, and 100% victory detection.

- [ ] **Step 5: Run map-control red**

Run:

```powershell
dotnet test tests/Game.Tests/Game.Tests.csproj --filter MapControlCalculatorTests
```

Expected: fails because map-control types do not exist.

- [ ] **Step 6: Implement minimal map-control calculation**

Produce ranked rows by controlled area percentage, then elimination count, then faction name.

- [ ] **Step 7: Run green**

Run:

```powershell
dotnet test tests/Game.Tests/Game.Tests.csproj --filter "CombatCalculatorTests|MapControlCalculatorTests"
```

Expected: all combat and map-control tests pass.

## Task 5: Match Match Service

**Files:**
- Create: `tests/Game.Tests/Application/MatchMatchServiceTests.cs`
- Create: `src/Game.Application/MatchDtos.cs`
- Create: `src/Game.Application/MatchMatchService.cs`

- [ ] **Step 1: Write failing tests**

Cover deterministic Cardiff defaults: 100 territories, 2 human factions, 6 NPC factions, 100 starting army strength, neutral remainder, at least one route, stats on every territory, leaderboard rows for every faction.

- [ ] **Step 2: Run red**

Run:

```powershell
dotnet test tests/Game.Tests/Game.Tests.csproj --filter MatchMatchServiceTests
```

Expected: fails because application service types do not exist.

- [ ] **Step 3: Implement minimal service**

Generate deterministic match data in memory and call domain calculators for stats and leaderboard output.

- [ ] **Step 4: Run green**

Run:

```powershell
dotnet test tests/Game.Tests/Game.Tests.csproj --filter MatchMatchServiceTests
```

Expected: match match tests pass.

## Task 6: Minimal API Match Endpoint

**Files:**
- Create: `tests/Game.Tests/Api/MatchApiTests.cs`
- Modify: `src/Game.Api/Program.cs`

- [ ] **Step 1: Write failing API test**

Use `WebApplicationFactory<Program>` to assert `GET /api/matches/cardiff` returns a match snapshot with Cardiff defaults.

- [ ] **Step 2: Run red**

Run:

```powershell
dotnet test tests/Game.Tests/Game.Tests.csproj --filter MatchApiTests
```

Expected: fails because the endpoint is not mapped.

- [ ] **Step 3: Implement endpoint**

Register `MatchMatchService` and map `GET /api/matches/cardiff`.

- [ ] **Step 4: Run green**

Run:

```powershell
dotnet test tests/Game.Tests/Game.Tests.csproj --filter MatchApiTests
```

Expected: API test passes.

## Final Verification

- [ ] Run:

```powershell
dotnet test Game.sln
```

Expected: all tests pass with exit code 0.

## Known Gaps After This Slice

- No MySQL persistence yet.
- No OSM ingestion yet.
- No SignalR realtime channel yet.
- No React/MapLibre client yet because Node/npm are not currently usable in this environment.
- No real territory polygon generation yet; match uses deterministic in-memory territory summaries.
