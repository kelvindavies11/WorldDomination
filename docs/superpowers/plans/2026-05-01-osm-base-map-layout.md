# OSM Base Map Layout Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first active match screen around a full-screen OSM base map for the Cardiff match.

**Architecture:** Keep the implementation in the existing static frontend served by `Game.Api`. Load MapLibre in `index.html`, render the map-first match layout in `app.js`, and keep the dark theme, full-screen map, floating widgets, and responsive rules in `styles.css`.

**Tech Stack:** ASP.NET Core static files, vanilla JavaScript modules, MapLibre GL JS CDN, CSS grid/flex layouts, xUnit API/static shell tests.

---

### Task 1: Static Shell Test

**Files:**
- Modify: `tests/Game.Tests/Api/MatchApiTests.cs`

- [ ] **Step 1: Write the failing test**

Add a test that creates a temporary static web root containing an index shell with `match-map-shell`, then requests `/games/cardiff` and asserts the shell is returned.

- [ ] **Step 2: Run the focused test**

Run: `dotnet test Game.sln --no-restore --filter FullyQualifiedName~MatchApiTests.CardiffMatchRouteReturnsFrontendShell`

Expected: fail before adding the test target or pass once the fallback behavior is confirmed.

### Task 2: Map Shell Markup And Script Loading

**Files:**
- Modify: `src/Game.Api/wwwroot/index.html`
- Modify: `src/Game.Api/wwwroot/app.js`

- [ ] **Step 1: Add MapLibre assets**

Load MapLibre CSS and JavaScript from `https://unpkg.com/maplibre-gl@^5.24.0/dist/`, matching the current MapLibre CDN quickstart pattern.

- [ ] **Step 2: Render `/games/cardiff` as the active match screen**

Replace the placeholder match page with a layout containing:

- `#match-map`
- `.match-layout`
- `.floating-widget.selected-territory-widget`
- `.floating-widget.leaderboard-widget`
- `data-action="toggle-widget"` controls

- [ ] **Step 3: Initialize the Cardiff map after render**

Create a MapLibre map centered near Cardiff, add navigation controls, constrain the camera to Cardiff bounds, render the irregular Cardiff playable-area polygon, grey out the out-of-bounds area, and show a readable fallback message if `maplibregl` is unavailable.

### Task 3: Command Map Styling

**Files:**
- Modify: `src/Game.Api/wwwroot/styles.css`

- [ ] **Step 1: Add full active-match layout styles**

Make the match page fill the viewport, keep the map as the page surface, and style the selected-territory and leaderboard panels as collapsible floating widgets.

- [ ] **Step 2: Add responsive styles**

On narrow screens, stack the panels around the map and keep the map height stable.

### Task 4: Verification

**Files:**
- No additional files.

- [ ] **Step 1: Stop any old `Game.Api` process that locks build output**

Run: `Get-Process Game.Api -ErrorAction SilentlyContinue | Stop-Process`

- [ ] **Step 2: Run the full test suite**

Run: `dotnet test Game.sln --no-restore`

Expected: all tests pass.

- [ ] **Step 3: Start the API**

Run: `dotnet run --project src/Game.Api/Game.Api.csproj --urls http://localhost:5057`

- [ ] **Step 4: Verify the route responds**

Open or request `http://localhost:5057/games/cardiff` and confirm the HTML shell is returned.
