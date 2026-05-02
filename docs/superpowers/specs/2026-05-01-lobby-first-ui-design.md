# Lobby-First UI Design

## Purpose

Build a working browser UI on top of the existing Cardiff match API. The first screen flow should lead with game creation, joining, and lobby setup rather than jumping straight into the active match.

The UI must respect the existing clean architecture: game rules and match snapshot generation stay in `Game.Domain` and `Game.Application`; `Game.Api` only serves HTTP/static delivery; the frontend consumes API DTOs instead of duplicating authoritative game logic.

## Approved Direction

Use the lobby-first direction from the visual options:

- `/` is a compact game dashboard with Create Game, Join Game, and Cardiff match entry points.
- `/games/create` exposes MVP match settings with Cardiff defaults.
- `/games/cardiff/lobby` is the primary first slice. It fetches `/api/matches/cardiff`, shows players, NPCs, match settings, and a map-style lobby preview.
- `/games/cardiff` exists as a simple active-match placeholder so the lobby has a valid "enter match" destination.

## Architecture

Add a frontend app under `src/Game.Web` using React, TypeScript, and Vite. Keep the UI data boundary explicit with a typed API client that fetches `MatchMatchSnapshot` from `/api/matches/cardiff`.

Modify `Game.Api` to serve built frontend assets from `wwwroot` and fall back to `index.html` for non-API routes. API routes under `/api/*` must continue returning JSON and must not be swallowed by the SPA fallback.

For development, the Vite app can run separately and proxy `/api` to the ASP.NET Core API. For production/local integrated runs, the Vite build output is copied into `src/Game.Api/wwwroot`.

## UI Scope

The first UI slice includes:

- shell navigation
- home dashboard
- create-game form with static MVP defaults
- lobby screen powered by the Cardiff match snapshot
- faction/player summary
- NPC count and territory count derived from the snapshot
- map-style territory preview using the 100 match territories
- selected HQ preview state in the client
- route/link to enter the active match placeholder
- loading and error states for the lobby snapshot request

The first UI slice does not include:

- real game creation persistence
- real join codes
- SignalR
- MapLibre
- movement commands
- combat commands
- authentication or anonymous session persistence

## Data Flow

The lobby fetches:

```text
GET /api/matches/cardiff -> MatchMatchSnapshot
```

The frontend derives:

- human players from `factions` where `kind === "Human"`
- NPC factions from `factions` where `kind === "Npc"`
- occupied starts from `territories[*].ownerFactionId`
- army locations from `armies[*].territoryId`
- route previews from `routes`
- leaderboard rows from `leaderboard`

The selected HQ is UI-only for this slice. It should not mutate backend state.

## Error Handling

The lobby should show:

- loading state while the snapshot is being fetched
- a readable error panel if the API request fails
- a retry button that reruns the request

The API should continue to return JSON for `/api/matches/cardiff`. Unknown `/api/*` routes may return normal ASP.NET Core 404 behavior. Unknown non-API paths should return the frontend shell.

## Testing

Backend/API tests:

- existing `/api/matches/cardiff` test must continue passing
- root `/` should return the frontend shell when static assets exist
- client routes such as `/games/cardiff/lobby` should return the frontend shell when static assets exist
- missing `/api/*` paths should not return the frontend shell

Frontend tests:

- API client builds the Cardiff match request path
- lobby model derivation separates humans and NPCs
- lobby model includes territory and route counts

Verification:

- `dotnet test Game.sln`
- frontend test command
- frontend build command
- run API and inspect the lobby route in a browser if local tooling permits

## Open Notes

`git` is not available on the current PATH, so this design cannot be committed from this environment unless Git becomes available.
