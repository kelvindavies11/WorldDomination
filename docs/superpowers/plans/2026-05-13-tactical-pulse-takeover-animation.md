# Tactical Pulse Takeover Animation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Tactical Pulse takeover animations and optional sounds for attacks, reinforcements, and claims, synchronized across multiplayer clients.

**Architecture:** Keep match state authoritative in the API and existing snapshots. Add a presentation-only SignalR event and focused browser modules for options, animation scaling, and sound playback. Reuse existing MapLibre sources/layers rather than moving game rules into the frontend.

**Tech Stack:** ASP.NET Core Minimal API + SignalR, dependency-free browser JavaScript modules, Node `node:test`, Playwright.

---

## File Structure

- Create `src/Game.Api/wwwroot/gameOptions.mjs`: loads, persists, toggles, and renders game option controls.
- Create `src/Game.Api/wwwroot/tacticalPulse.mjs`: calculates duration/intensity and records active animation state for tests and runtime.
- Create `src/Game.Api/wwwroot/tacticalPulseSound.mjs`: generated Web Audio cues for attack, reinforce, and claim.
- Modify `src/Game.Api/wwwroot/app.js`: wire options into the game menu, listen for `TerritoryActionResolved`, and run Tactical Pulse effects through existing MapLibre helpers.
- Modify `src/Game.Api/Program.cs`: broadcast `TerritoryActionResolved` for start-position claims and player movement results.
- Add `tests/ui/gameOptions.test.mjs`.
- Add `tests/ui/tacticalPulse.test.mjs`.
- Modify `tests/playwright/specs/territory-sync.spec.js`: assert both player pages display the same animation event metadata after a movement.
- Modify `README.md` and `docs/technical-architecture.md`.

## Tasks

### Task 1: Game Options Module

**Files:**
- Create: `src/Game.Api/wwwroot/gameOptions.mjs`
- Test: `tests/ui/gameOptions.test.mjs`

- [ ] Write failing tests for default enabled options, persisted disabled options, toggling, and markup names.
- [ ] Run `node --test tests/ui/gameOptions.test.mjs` and confirm it fails because the module does not exist.
- [ ] Implement `defaultGameOptions`, `loadGameOptions(storage)`, `setGameOption(storage, key, enabled)`, and `gameOptionsMarkup(options)`.
- [ ] Run `node --test tests/ui/gameOptions.test.mjs` and confirm it passes.

### Task 2: Tactical Pulse Scaling Module

**Files:**
- Create: `src/Game.Api/wwwroot/tacticalPulse.mjs`
- Test: `tests/ui/tacticalPulse.test.mjs`

- [ ] Write failing tests for duration scaling: strength 1 is faster than strength 40, capped at a maximum, and reinforcement uses softer intensity than attack.
- [ ] Run `node --test tests/ui/tacticalPulse.test.mjs` and confirm it fails because the module does not exist.
- [ ] Implement `tacticalPulseTiming(actionType, strength)` and `createTacticalPulseEventState(event, options)`.
- [ ] Run `node --test tests/ui/tacticalPulse.test.mjs` and confirm it passes.

### Task 3: API Realtime Presentation Event

**Files:**
- Modify: `src/Game.Api/Program.cs`
- Test: `tests/Game.Tests/Api/MatchApiTests.cs`

- [ ] Add an API test that exercises a movement endpoint and verifies the response still succeeds with the existing snapshot contract.
- [ ] Run `dotnet test Game.sln --filter MatchApiTests` and confirm no production event code has been added yet.
- [ ] Add a local helper in `Program.cs` to broadcast `TerritoryActionResolved` after accepted start-position and movement commands.
- [ ] Run `dotnet test Game.sln --filter MatchApiTests` and confirm it passes.

### Task 4: Browser Wiring, Animation, And Sound

**Files:**
- Create: `src/Game.Api/wwwroot/tacticalPulseSound.mjs`
- Modify: `src/Game.Api/wwwroot/app.js`
- Test: `tests/ui/gameOptions.test.mjs`, `tests/ui/tacticalPulse.test.mjs`

- [ ] Add tests for markup and scaling before browser wiring.
- [ ] Import the new modules in `app.js`.
- [ ] Add game option toggles to the existing game menu.
- [ ] Add a `TerritoryActionResolved` SignalR handler that stores `window.__lastTacticalPulseEvent__` and triggers visual/sound effects when enabled.
- [ ] Respect disabled animations and disabled sounds independently.
- [ ] Run `node --test tests/ui/*.test.mjs`.

### Task 5: Multiplayer Playwright Coverage

**Files:**
- Modify: `tests/playwright/specs/territory-sync.spec.js`

- [ ] Extend the existing two-browser movement section to wait until both pages expose `window.__lastTacticalPulseEvent__`.
- [ ] Assert both pages have the same `targetTerritoryId`, `actionType`, and `strength`.
- [ ] Run `node tests/playwright/node_modules/@playwright/test/cli.js test tests/playwright/specs/territory-sync.spec.js`.

### Task 6: Documentation And Full Verification

**Files:**
- Modify: `README.md`
- Modify: `docs/technical-architecture.md`

- [ ] Document Tactical Pulse behavior and game options.
- [ ] Run `dotnet test Game.sln`.
- [ ] Run `node --test tests/ui/*.test.mjs`.
- [ ] Run the Playwright territory sync spec with the API server available.
