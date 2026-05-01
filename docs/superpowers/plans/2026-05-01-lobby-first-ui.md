# Lobby-First UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a working lobby-first React UI on top of the existing Cardiff prototype API and serve it from the ASP.NET Core API.

**Architecture:** Add a focused Vite/React frontend in `src/Game.Web` with a typed API client and small view-model helpers. Update `Game.Api` only for static frontend delivery and SPA fallback routing; game data remains owned by the existing application service and API endpoint.

**Tech Stack:** .NET 10, ASP.NET Core Minimal APIs, React, TypeScript, Vite, Vitest, CSS modules/plain CSS.

---

## File Structure

- Create `src/Game.Web/package.json`: frontend scripts and dependencies.
- Create `src/Game.Web/index.html`: Vite HTML entry.
- Create `src/Game.Web/src/main.tsx`: React app bootstrap.
- Create `src/Game.Web/src/App.tsx`: route selection and page composition.
- Create `src/Game.Web/src/api/prototype.ts`: typed DTOs and fetch client for `/api/prototype/cardiff`.
- Create `src/Game.Web/src/features/lobby/lobbyModel.ts`: pure derivation from snapshot to lobby model.
- Create `src/Game.Web/src/features/lobby/lobbyModel.test.ts`: frontend unit coverage.
- Create `src/Game.Web/src/styles.css`: command-map UI styling.
- Modify `src/Game.Api/Program.cs`: serve static files, default files, and SPA fallback for non-API routes.
- Modify `tests/Game.Tests/Api/PrototypeApiTests.cs`: add fallback/static behavior tests using temporary static web root.
- Modify `README.md`: add UI setup, build, and integrated run notes.

### Task 1: Backend SPA Fallback Tests

**Files:**
- Modify: `tests/Game.Tests/Api/PrototypeApiTests.cs`

- [ ] **Step 1: Write failing API/static routing tests**

Add tests that create a temporary `wwwroot/index.html`, configure `WebApplicationFactory` to use that content root, and verify:

```csharp
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
```

Add helper code in the same test file:

```csharp
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
```

- [ ] **Step 2: Run tests and verify they fail**

Run: `dotnet test tests/Game.Tests/Game.Tests.csproj --filter PrototypeApiTests`

Expected: the new root/client-route tests fail because `Program.cs` still redirects `/` to `/api/prototype/cardiff` and has no SPA fallback.

### Task 2: Backend Static Delivery

**Files:**
- Modify: `src/Game.Api/Program.cs`

- [ ] **Step 1: Implement minimal static file and fallback routing**

Replace the root redirect with static/default file handling and a non-API fallback:

```csharp
using Game.Application;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<PrototypeMatchService>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/prototype/cardiff", (PrototypeMatchService service) =>
    Results.Ok(service.CreateCardiffPrototype()));

app.MapFallback(context =>
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return Task.CompletedTask;
    }

    context.Response.ContentType = "text/html; charset=utf-8";
    return context.Response.SendFileAsync(Path.Combine(app.Environment.WebRootPath, "index.html"));
});

app.Run();

public partial class Program;
```

- [ ] **Step 2: Run backend tests and verify they pass**

Run: `dotnet test tests/Game.Tests/Game.Tests.csproj --filter PrototypeApiTests`

Expected: all `PrototypeApiTests` pass.

### Task 3: Frontend Scaffold And Model Tests

**Files:**
- Create: `src/Game.Web/package.json`
- Create: `src/Game.Web/index.html`
- Create: `src/Game.Web/src/api/prototype.ts`
- Create: `src/Game.Web/src/features/lobby/lobbyModel.ts`
- Create: `src/Game.Web/src/features/lobby/lobbyModel.test.ts`

- [ ] **Step 1: Create frontend package and test target**

`package.json`:

```json
{
  "scripts": {
    "dev": "vite --host 127.0.0.1 --port 5173",
    "build": "tsc -b && vite build --outDir ../Game.Api/wwwroot --emptyOutDir",
    "test": "vitest run"
  },
  "dependencies": {
    "@vitejs/plugin-react": "^latest",
    "vite": "^latest",
    "typescript": "^latest",
    "react": "^latest",
    "react-dom": "^latest"
  },
  "devDependencies": {
    "vitest": "^latest",
    "@types/react": "^latest",
    "@types/react-dom": "^latest"
  }
}
```

- [ ] **Step 2: Add failing lobby model tests**

`lobbyModel.test.ts`:

```ts
import { describe, expect, it } from "vitest";
import { createLobbyModel } from "./lobbyModel";
import type { PrototypeMatchSnapshot } from "../../api/prototype";

const snapshot: PrototypeMatchSnapshot = {
  gameId: "cardiff-prototype",
  mapArea: "Cardiff",
  factions: [
    { id: "human-1", name: "Player 1", kind: "Human", color: "#1f8a70" },
    { id: "human-2", name: "Player 2", kind: "Human", color: "#2f6fbd" },
    { id: "npc-1", name: "NPC 1", kind: "Npc", color: "#c58a1a" }
  ],
  territories: [
    { id: "territory-000", index: 0, name: "Cardiff Sector 1", areaSquareKm: 1, ownerFactionId: "human-1", stats: { economy: 10, defense: 20, mobility: 30, strategicValue: 40 } },
    { id: "territory-001", index: 1, name: "Cardiff Sector 2", areaSquareKm: 1.2, ownerFactionId: null, stats: { economy: 11, defense: 21, mobility: 31, strategicValue: 41 } }
  ],
  armies: [
    { id: "army-human-1", factionId: "human-1", territoryId: "territory-000", strength: 100 }
  ],
  routes: [
    { sourceTerritoryId: "territory-000", destinationTerritoryId: "territory-001", transport: "Road", etaSeconds: 90, isAllowed: true }
  ],
  leaderboard: []
};

describe("createLobbyModel", () => {
  it("separates human players and NPC factions", () => {
    const model = createLobbyModel(snapshot);

    expect(model.humanPlayers.map(player => player.name)).toEqual(["Player 1", "Player 2"]);
    expect(model.npcFactions.map(faction => faction.name)).toEqual(["NPC 1"]);
  });

  it("summarizes territory and route counts", () => {
    const model = createLobbyModel(snapshot);

    expect(model.summary).toEqual({
      mapArea: "Cardiff",
      territories: 2,
      armies: 1,
      routes: 1,
      occupiedStarts: 1
    });
  });
});
```

- [ ] **Step 3: Run frontend tests and verify they fail**

Run from `src/Game.Web`: `npm test`

Expected: tests fail because `createLobbyModel` does not exist.

- [ ] **Step 4: Implement DTOs and lobby model**

`prototype.ts`:

```ts
export type FactionKind = "Human" | "Npc";

export type TerritoryStats = {
  economy: number;
  defense: number;
  mobility: number;
  strategicValue: number;
};

export type PrototypeFaction = {
  id: string;
  name: string;
  kind: FactionKind;
  color: string;
};

export type PrototypeTerritory = {
  id: string;
  index: number;
  name: string;
  areaSquareKm: number;
  ownerFactionId: string | null;
  stats: TerritoryStats;
};

export type PrototypeArmy = {
  id: string;
  factionId: string;
  territoryId: string;
  strength: number;
};

export type PrototypeRoute = {
  sourceTerritoryId: string;
  destinationTerritoryId: string;
  transport: string;
  etaSeconds: number;
  isAllowed: boolean;
};

export type LeaderboardRow = {
  factionId: string;
  factionName: string;
  controlledAreaSquareKm: number;
  controlPercentage: number;
  eliminationCount: number;
  rank: number;
};

export type PrototypeMatchSnapshot = {
  gameId: string;
  mapArea: string;
  factions: PrototypeFaction[];
  territories: PrototypeTerritory[];
  armies: PrototypeArmy[];
  routes: PrototypeRoute[];
  leaderboard: LeaderboardRow[];
};

export async function fetchCardiffPrototype(signal?: AbortSignal): Promise<PrototypeMatchSnapshot> {
  const response = await fetch("/api/prototype/cardiff", { signal });

  if (!response.ok) {
    throw new Error(`Cardiff prototype request failed with ${response.status}`);
  }

  return response.json() as Promise<PrototypeMatchSnapshot>;
}
```

`lobbyModel.ts`:

```ts
import type { PrototypeMatchSnapshot } from "../../api/prototype";

export function createLobbyModel(snapshot: PrototypeMatchSnapshot) {
  const humanPlayers = snapshot.factions.filter(faction => faction.kind === "Human");
  const npcFactions = snapshot.factions.filter(faction => faction.kind === "Npc");
  const occupiedStarts = snapshot.territories.filter(territory => territory.ownerFactionId !== null).length;

  return {
    humanPlayers,
    npcFactions,
    summary: {
      mapArea: snapshot.mapArea,
      territories: snapshot.territories.length,
      armies: snapshot.armies.length,
      routes: snapshot.routes.length,
      occupiedStarts
    }
  };
}
```

- [ ] **Step 5: Run frontend tests and verify they pass**

Run from `src/Game.Web`: `npm test`

Expected: lobby model tests pass.

### Task 4: Frontend Screens

**Files:**
- Create: `src/Game.Web/src/main.tsx`
- Create: `src/Game.Web/src/App.tsx`
- Create: `src/Game.Web/src/styles.css`
- Modify: `src/Game.Web/index.html`

- [ ] **Step 1: Add React app shell and lobby pages**

Implement:

- home dashboard
- create game form with static defaults
- lobby page that calls `fetchCardiffPrototype`
- active match placeholder
- route selection from `window.location.pathname`

- [ ] **Step 2: Add responsive command-map CSS**

Implement layout with:

- compact top navigation
- map preview as a CSS grid of territories
- side panels for players and settings
- bottom/stacked mobile layout
- loading/error/retry states

- [ ] **Step 3: Run frontend tests**

Run from `src/Game.Web`: `npm test`

Expected: tests pass.

- [ ] **Step 4: Build frontend into API wwwroot**

Run from `src/Game.Web`: `npm run build`

Expected: `src/Game.Api/wwwroot/index.html` and assets are generated.

### Task 5: Integrated Verification And Docs

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Update README commands**

Add:

```markdown
## Run UI

```powershell
cd src/Game.Web
npm install
npm run build
cd ../..
dotnet run --project src/Game.Api/Game.Api.csproj --urls http://localhost:5057
```

Then open:

```text
http://localhost:5057/games/cardiff/lobby
```
```

- [ ] **Step 2: Run full backend test suite**

Run: `dotnet test Game.sln`

Expected: all backend tests pass.

- [ ] **Step 3: Run frontend test and build**

Run from `src/Game.Web`:

```powershell
npm test
npm run build
```

Expected: frontend tests pass and build succeeds.

- [ ] **Step 4: Run API and inspect lobby route**

Run:

```powershell
dotnet run --project src/Game.Api/Game.Api.csproj --urls http://localhost:5057
```

Open:

```text
http://localhost:5057/games/cardiff/lobby
```

Expected: lobby page renders, fetches Cardiff prototype data, and shows the lobby preview.

## Self-Review

- Spec coverage: plan covers lobby-first routes, typed API fetch, UI-only HQ preview, backend SPA fallback, tests, and docs.
- Placeholder scan: no `TBD` or vague implementation-only steps remain.
- Type consistency: DTO names and route paths match the approved design.
