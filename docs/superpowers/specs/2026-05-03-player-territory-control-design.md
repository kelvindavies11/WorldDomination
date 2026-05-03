# Player Territory Control Design

## Purpose

Add the first player-facing territory-control slice for the Cardiff match. The goal is to let a human player expand from an owned territory into a neighboring neutral territory through a backend-authoritative command flow.

This slice intentionally excludes enemy combat, reinforcement timing, NPC decisions, persistence, and full realtime simulation. It proves the core player interaction first while keeping the existing clean architecture boundaries intact.

## Selected Approach

Use a player-first neutral capture slice.

The player selects one of their owned territories, selects a valid neighboring neutral territory, chooses troop strength with a slider, and sends the order. The backend validates the command and applies the neutral capture result. The UI updates from the backend response.

This approach is preferred because it makes expansion playable quickly without letting the frontend own canonical match state.

## Player Flow

1. The player opens the Cardiff match map.
2. The player clicks one of their own territories.
3. The selected territory panel shows ownership, stats, available army strength, and an expansion action state.
4. Valid neighboring neutral territories become selectable targets on the map.
5. The player clicks a valid neutral target territory.
6. The side panel changes to a move-order view showing source, destination, route type, ETA, available troops, and a troop slider.
7. The player chooses troop strength with the slider and presses Send.
8. The backend validates the order.
9. If accepted, the neutral territory is captured by the player for this first slice.
10. The source army is reduced, the captured territory receives the sent army, and the leaderboard updates.

Invalid clicks should stay calm. Enemy territories, non-neighboring territories, territories without an army, or territories not owned by the player should inspect the territory or show a short disabled reason instead of sending an order.

## Architecture

### Game.Domain

Add pure rules for player expansion and neutral capture. The domain should answer:

- whether the source territory is owned by the acting faction
- whether the target territory is neutral
- whether a route exists between source and target
- whether the requested strength is between 1 and the available army strength
- what the army and ownership result should be after a neutral capture

Domain code must not depend on ASP.NET Core, frontend code, persistence, or map rendering.

### Game.Application

Add an application service for the use case, such as `PlayerTerritoryCommandService`.

The service should accept a command containing:

- match id
- player faction id
- source territory id
- target territory id
- strength

The service should validate the command through domain rules, apply the neutral capture result to current Cardiff match state, recalculate the leaderboard, and return an explicit DTO.

The current Cardiff match service creates deterministic snapshots rather than persisted live state. For this slice, introduce an in-memory Cardiff match state service so the API can return changed ownership and armies after a command. This should remain an application-level MVP adapter and should not be treated as final persistence.

### Game.Api

Add a Minimal API command endpoint:

```text
POST /api/matches/cardiff/movements
```

Request:

```json
{
  "playerFactionId": "human-1",
  "sourceTerritoryId": "postcode-cf10",
  "targetTerritoryId": "postcode-cf11",
  "strength": 40
}
```

The endpoint should return `400 Bad Request` with a clear error for invalid commands.

For valid commands, return an accepted movement or capture result plus updated match state. Returning the updated snapshot is acceptable for this slice because the match is small and the existing UI already consumes snapshots.

### Static UI

Update `src/Game.Api/wwwroot/app.js` and related styles to support:

- selecting an owned source territory
- highlighting or otherwise marking valid neutral target territories
- selecting a neutral target
- showing a move-order panel with source, target, route type, ETA, available army, and a troop slider
- submitting the command to the API
- updating the map ownership colors, selected territory panel, army display, and leaderboard from the backend response

The UI may derive target affordances from the current backend snapshot and route list, but it must not treat local state as authoritative.

## Data Flow

1. `GET /api/matches/cardiff` loads the current match snapshot.
2. The player selects an owned source territory.
3. The UI derives selectable neutral targets from backend-provided routes and current ownership.
4. The player selects a target and troop strength.
5. `POST /api/matches/cardiff/movements` sends the command.
6. Application validation checks faction, source ownership, route, target neutrality, and strength.
7. Valid command applies neutral capture and recalculates leaderboard.
8. API returns updated state.
9. UI refreshes panels, map ownership, armies, and leaderboard from the response.

## Error Handling

Invalid commands should produce explicit errors:

- source territory is not owned by the acting faction
- source territory has no army
- strength must be at least 1
- strength cannot exceed the available source army strength
- target territory is not connected to the source
- target territory is not neutral in this first slice
- match not found, reserved for later multi-match support

The UI should display these as concise panel messages and leave the previous match state intact.

## Automated Tests

Add automated tests for the player scenarios:

- a player can use one of their owned territories as a source
- a neighboring neutral territory is a valid expansion target
- a non-neighboring territory is rejected
- a target owned by another faction is rejected for this slice
- a source without enough army strength is rejected
- a valid move command returns an accepted result with an ETA or route details
- neutral capture changes target ownership to the acting faction
- source army strength is reduced by the sent strength
- captured territory receives the arriving army
- leaderboard map-control percentage recalculates after capture
- API returns `400 Bad Request` for invalid movement commands
- API returns updated state for valid movement commands

Most behavioral coverage belongs in `tests/Game.Tests/Application`. Pure validation and capture-result rules should be tested in `tests/Game.Tests/Domain` if implemented as domain services. Endpoint behavior belongs in `tests/Game.Tests/Api`.

If UI helper logic becomes pure and testable, add focused JavaScript tests. Gameplay correctness must remain backend-tested because the backend is authoritative.

## Verification

After implementation, run:

```powershell
dotnet test Game.sln
```

If UI helper tests are added, also run the existing JavaScript test command used by the project.

## Non-Goals

This slice does not include:

- enemy territory combat
- NPC expansion
- full movement countdown simulation
- SignalR realtime updates
- MySQL persistence
- battle reinforcements
- eliminations
- victory checks beyond existing leaderboard recalculation
