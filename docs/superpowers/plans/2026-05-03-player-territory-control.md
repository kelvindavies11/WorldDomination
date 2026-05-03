# Player Territory Control Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first backend-authoritative player expansion flow: select an owned Cardiff territory, send troops to a connected neutral territory, capture it, and refresh armies and leaderboard.

**Architecture:** Put pure validation and capture rules in `Game.Domain`, orchestrate mutable MVP match state in `Game.Application`, expose the command through `Game.Api`, and keep the static browser UI as a command surface. The backend remains authoritative; the UI only derives affordances from the latest API snapshot.

**Tech Stack:** .NET 10, ASP.NET Core Minimal APIs, xUnit, dependency-free static JavaScript/CSS served from `Game.Api/wwwroot`.

---

### Task 1: Domain Expansion Rules

**Files:**
- Create: `src/Game.Domain/TerritoryExpansion.cs`
- Test: `tests/Game.Tests/Domain/TerritoryExpansionTests.cs`

- [ ] **Step 1: Write failing validation and capture tests**

```csharp
using Game.Domain;

namespace Game.Tests.Domain;

public sealed class TerritoryExpansionTests
{
    [Fact]
    public void AllowsOwnedSourceToCaptureConnectedNeutralTarget()
    {
        var result = TerritoryExpansion.ValidateNeutralCapture(new NeutralCaptureRequest(
            ActingFactionId: "human-1",
            SourceTerritoryId: "source",
            SourceOwnerFactionId: "human-1",
            TargetTerritoryId: "target",
            TargetOwnerFactionId: null,
            AvailableArmyStrength: 100,
            RequestedStrength: 40,
            HasAllowedRoute: true));

        Assert.True(result.IsValid);
        Assert.Null(result.Error);
    }

    [Fact]
    public void RejectsNonNeighboringTarget()
    {
        var result = TerritoryExpansion.ValidateNeutralCapture(new NeutralCaptureRequest(
            "human-1", "source", "human-1", "target", null, 100, 40, HasAllowedRoute: false));

        Assert.False(result.IsValid);
        Assert.Equal("Target territory is not connected to the source.", result.Error);
    }

    [Fact]
    public void RejectsEnemyTargetForNeutralCaptureSlice()
    {
        var result = TerritoryExpansion.ValidateNeutralCapture(new NeutralCaptureRequest(
            "human-1", "source", "human-1", "target", "npc-1", 100, 40, HasAllowedRoute: true));

        Assert.False(result.IsValid);
        Assert.Equal("Target territory is not neutral in this first slice.", result.Error);
    }

    [Fact]
    public void RejectsStrengthAboveAvailableArmy()
    {
        var result = TerritoryExpansion.ValidateNeutralCapture(new NeutralCaptureRequest(
            "human-1", "source", "human-1", "target", null, 30, 40, HasAllowedRoute: true));

        Assert.False(result.IsValid);
        Assert.Equal("Strength cannot exceed the available source army strength.", result.Error);
    }

    [Fact]
    public void AppliesNeutralCaptureArmyResult()
    {
        var result = TerritoryExpansion.ApplyNeutralCapture(new NeutralCaptureArmyState(
            ActingFactionId: "human-1",
            SourceArmyStrength: 100,
            RequestedStrength: 40));

        Assert.Equal("human-1", result.TargetOwnerFactionId);
        Assert.Equal(60, result.SourceArmyStrength);
        Assert.Equal(40, result.TargetArmyStrength);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Game.Tests/Game.Tests.csproj --filter TerritoryExpansionTests`

Expected: FAIL because `TerritoryExpansion` and related records do not exist.

- [ ] **Step 3: Add minimal domain implementation**

Create `src/Game.Domain/TerritoryExpansion.cs` with `NeutralCaptureRequest`, `NeutralCaptureValidationResult`, `NeutralCaptureArmyState`, `NeutralCaptureResult`, and `TerritoryExpansion`.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/Game.Tests/Game.Tests.csproj --filter TerritoryExpansionTests`

Expected: PASS.

### Task 2: Application Command Service

**Files:**
- Modify: `src/Game.Application/MatchDtos.cs`
- Create: `src/Game.Application/CardiffMatchStateService.cs`
- Create: `src/Game.Application/PlayerTerritoryCommandService.cs`
- Test: `tests/Game.Tests/Application/PlayerTerritoryCommandServiceTests.cs`

- [ ] **Step 1: Write failing application tests**

Test valid neutral capture, rejected non-neighbor target, rejected enemy target, rejected over-strength command, source army reduction, target army creation, and leaderboard update.

- [ ] **Step 2: Run application tests to verify they fail**

Run: `dotnet test tests/Game.Tests/Game.Tests.csproj --filter PlayerTerritoryCommandServiceTests`

Expected: FAIL because the command service does not exist.

- [ ] **Step 3: Add DTOs and in-memory Cardiff state**

Add request/result DTOs to `MatchDtos.cs`. Add `CardiffMatchStateService` that lazily initializes one Cardiff snapshot, returns it from GET, and applies replacement snapshots after valid commands.

- [ ] **Step 4: Add command orchestration**

Add `PlayerTerritoryCommandService.SendArmyToNeutralTerritory(...)`, using domain validation, current snapshot routes, current armies, neutral capture application, and leaderboard recalculation.

- [ ] **Step 5: Run application tests**

Run: `dotnet test tests/Game.Tests/Game.Tests.csproj --filter PlayerTerritoryCommandServiceTests`

Expected: PASS.

### Task 3: API Endpoint

**Files:**
- Modify: `src/Game.Api/Program.cs`
- Test: `tests/Game.Tests/Api/MatchApiTests.cs`

- [ ] **Step 1: Write failing API tests**

Add tests that `POST /api/matches/cardiff/movements` returns `400 Bad Request` for invalid movement and `200 OK` with an updated snapshot for a valid neutral capture.

- [ ] **Step 2: Run API tests to verify they fail**

Run: `dotnet test tests/Game.Tests/Game.Tests.csproj --filter "MatchApiTests&CardiffMovement"`

Expected: FAIL because the endpoint does not exist.

- [ ] **Step 3: Wire services and endpoint**

Register `CardiffMatchStateService` and `PlayerTerritoryCommandService`. Change `GET /api/matches/cardiff` to return current state. Add `POST /api/matches/cardiff/movements`.

- [ ] **Step 4: Run API tests**

Run: `dotnet test tests/Game.Tests/Game.Tests.csproj --filter MatchApiTests`

Expected: PASS.

### Task 4: Static UI Command Flow

**Files:**
- Modify: `src/Game.Api/wwwroot/app.js`
- Modify: `src/Game.Api/wwwroot/styles.css`
- Test: `tests/Game.Tests/Api/MatchApiTests.cs`

- [ ] **Step 1: Write failing asset contract test**

Extend the frontend asset test to assert the presence of command flow hooks: `selectedSourceTerritoryId`, `selectedTargetTerritoryId`, `data-action="send-movement"`, `armyStrengthSlider`, `/api/matches/cardiff/movements`, and valid-target layer styling.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Game.Tests/Game.Tests.csproj --filter CardiffMatchFrontendAssetDeclaresTerritoryControlHooks`

Expected: FAIL because the hooks do not exist.

- [ ] **Step 3: Implement UI flow**

Update state, territory click handling, selected panel markup, command submission, and map layer filters so player-owned source selection can choose valid neutral targets and submit a slider-based movement command.

- [ ] **Step 4: Run frontend asset test**

Run: `dotnet test tests/Game.Tests/Game.Tests.csproj --filter CardiffMatchFrontendAssetDeclaresTerritoryControlHooks`

Expected: PASS.

### Task 5: Full Verification

**Files:**
- No new files.

- [ ] **Step 1: Run full test suite**

Run: `dotnet test Game.sln`

Expected: PASS.

- [ ] **Step 2: Review changed files**

Run a placeholder-marker scan across touched source, test, and plan files while excluding build outputs.

Expected: No new placeholder markers in touched files.
