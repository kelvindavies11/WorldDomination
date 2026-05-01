# Dynamic OSM World Domination Design

## Purpose

Build a real-time multiplayer world-domination strategy game using OpenStreetMap as the visible playing field.

The first version should prove the core loop: generated territories over a real map, human and NPC starting positions, army movement with ETAs, combat, permanent elimination, leaderboard tracking, and 100% map-control victory.

The canonical rules source is `docs/game-rules.md`. If this design and the rules file disagree, use `docs/game-rules.md` unless the user explicitly approves a rule change.

The technical architecture source is `docs/technical-architecture.md`. If this design and the technical architecture file disagree on implementation structure, use `docs/technical-architecture.md` unless the user explicitly approves a technical change.

## MVP Scope

The MVP includes:

- one match played on Cardiff as the prototype map area
- OpenStreetMap visuals as the base map
- random territory overlays of varied size
- territory stats calculated from OSM features
- human starting HQ selection with no balance filtering
- randomly but evenly spread NPC starting HQs
- one starting army per faction
- territory-to-territory army movement
- visible ETAs for all movement
- OSM-aware movement rules for roads, rail, bridges, tunnels, mountains, water, airports, and ports
- neutral territory capture
- enemy territory combat
- nearby battle reinforcement through normal movement and ETA rules
- permanent elimination
- elimination credit on the leaderboard
- live leaderboard by map-control percentage
- 100% map-control victory

Default prototype settings:

- map area: Cardiff
- territory count target: 80-120 territories, with 100 as the initial target
- human players: 2 for first testing, expandable to 4
- NPC factions: 6
- starting army strength: 100
- neutral territories: all territories not chosen or assigned as starts
- target match duration: 20-45 minutes for early testing

The MVP excludes:

- diplomacy
- alliances
- fog of war
- multiple unit types
- player-built buildings
- manual economy management
- respawning
- naval combat beyond port-to-port travel
- air combat beyond airport-to-airport movement

## Architecture

The game should use an authoritative backend simulation. The server owns match state, validates movement, resolves combat, runs NPC decisions, calculates leaderboard state, and broadcasts updates to clients.

The frontend should render the OpenStreetMap view, territory overlays, faction colors, armies, movement routes, ETAs, battle indicators, territory stats, and leaderboard state. The frontend may preview possible routes, but the backend must validate final orders.

The backend should follow clean architecture. Domain rules must not depend on ASP.NET Core, EF Core, MySQL, SignalR, or frontend concerns.

Recommended backend project boundaries:

- `Game.Domain`: entities, value objects, domain services, formulas, combat rules, movement rules, victory rules, and domain events
- `Game.Application`: use cases, commands, queries, DTOs, interfaces, orchestration, NPC decision application services, and simulation tick coordination
- `Game.Infrastructure`: EF Core, MySQL persistence, spatial query adapters, OSM ingestion adapters, external services, repositories, and clock/random implementations
- `Game.Api`: ASP.NET Core Minimal APIs, SignalR hubs, authentication, request/response mapping, and dependency injection composition
- `Game.Tests`: domain tests, application tests, integration tests, and API tests

Dependency direction:

```text
Game.Api -> Game.Application -> Game.Domain
Game.Infrastructure -> Game.Application
Game.Infrastructure -> Game.Domain
Game.Domain -> no project dependencies
```

The `Game.Application` layer should define interfaces for persistence, spatial operations, OSM feature lookup, realtime notifications, time, and randomness. `Game.Infrastructure` should implement those interfaces.

The simulation engine should live in `Game.Domain` and `Game.Application`:

- pure formulas and rule calculations belong in `Game.Domain`
- match orchestration and simulation ticks belong in `Game.Application`
- persistence, SignalR broadcasting, and MySQL spatial queries belong in `Game.Infrastructure` or `Game.Api`

The frontend should treat the backend as authoritative. It may display previews and optimistic UI hints, but it must not be the source of truth for movement, combat, territory ownership, eliminations, or victory.

## Tech Stack

Use a .NET backend and a modern TypeScript frontend.

Backend:

- .NET 10 LTS
- ASP.NET Core 10
- ASP.NET Core Minimal APIs for HTTP endpoints
- ASP.NET Core SignalR for realtime match updates
- C# domain/application layers for the simulation engine, rules, NPC behavior, combat, movement, and leaderboard logic
- Entity Framework Core with a MySQL provider for relational persistence
- MySQL 8.4 LTS or newer for persistence
- MySQL spatial data types, spatial indexes, SRIDs, and `ST_*` spatial functions for territory polygons, map bounds, and OSM-derived feature summaries
- NetTopologySuite in the C# domain/application layer for geometry calculations where useful

The data access layer should hide provider-specific spatial behavior behind repositories/services. If EF Core provider spatial translation is insufficient for a feature, use MySQL-native `ST_*` functions through focused SQL queries rather than leaking database-specific logic into the game rules engine.

FastAPI gateway layer:

- A Python FastAPI service may be used as an edge API/BFF layer if needed.
- FastAPI can expose client-facing HTTP endpoints, lightweight orchestration endpoints, admin/debug APIs, prototype endpoints, or AI/analytics-related endpoints.
- FastAPI should use modular routers for larger application structure.
- FastAPI may use WebSockets for prototype or auxiliary realtime flows, but the primary realtime game channel should remain ASP.NET Core SignalR unless explicitly changed.
- FastAPI must not own authoritative match state, combat resolution, movement validation, NPC behavior, eliminations, or victory checks.
- FastAPI should call the .NET backend through HTTP/gRPC/message contracts or publish requests through a queue, depending on the final deployment shape.
- FastAPI must be treated as an outer adapter. It must not import or duplicate game rules that belong in `Game.Domain`.

Default responsibility split:

```text
React/Vite client
  -> FastAPI gateway/BFF for optional client-facing aggregation or auxiliary APIs
  -> ASP.NET Core API/SignalR for authoritative game commands and realtime match state
  -> Game.Application/Game.Domain for rules and simulation
  -> Game.Infrastructure/MySQL for persistence and spatial data
```

Frontend:

- React 19
- TypeScript
- Vite
- MapLibre GL JS for OSM-based map rendering, territory overlays, markers, routes, and live map visuals
- SignalR TypeScript client for realtime updates
- TanStack Query for server-state fetching and caching
- lightweight local React state for selected territory, selected army, hovered route, panels, and UI-only state
- Tailwind CSS v4 or plain CSS modules for styling; choose one when the frontend is scaffolded and keep the UI consistent

Testing:

- xUnit or NUnit for backend domain and application tests
- EF Core integration tests for persistence and spatial queries
- frontend component tests after the UI is scaffolded
- Playwright browser tests for map rendering, route previews, and critical player flows

The backend should keep the rules engine independent from ASP.NET Core and EF Core. The simulation should be testable as pure C# application/domain code without starting the web server.

Recommended high-level components:

- Map ingestion and feature extraction
- Territory generation
- Territory stat calculation
- Match setup
- Simulation engine
- Movement and route validation
- Combat resolver
- NPC controller
- Leaderboard service
- SignalR realtime update gateway
- Frontend map UI

## Data Model

Core entities:

- `Game`: match id, map bounds, status, started/ended timestamps, winning faction
- `Faction`: human or NPC, name/color, eliminated status, elimination count
- `Player`: user identity, linked faction, connection state
- `Territory`: polygon, area, owner, neighbors, OSM feature summary, Economy, Defense, Mobility, Strategic Value
- `Army`: faction, current territory or active movement, strength
- `MovementOrder`: source, destination, route type, ETA, departure/arrival timestamps
- `Battle`: target territory, attacker faction, defender faction, participating armies, status
- `LeaderboardRow`: faction, map-control percentage, rank, elimination count, eliminated status

## Game Flow

1. Create a match for a chosen map area.
2. Load OSM visuals and feature data.
3. Generate random territory overlays.
4. Build territory adjacency and valid movement connections.
5. Calculate territory stats using `docs/game-rules.md`.
6. Let human players select any starting HQ.
7. Place NPC HQs randomly but evenly across the territory graph.
8. Spawn one starting army for each faction.
9. Start the realtime simulation.
10. Accept human movement orders and NPC movement orders.
11. Resolve movement ETAs.
12. Capture neutral territories or start battles on arrival.
13. Allow reinforcements to join battles through normal movement.
14. Eliminate factions with zero territories.
15. Update and broadcast leaderboard changes.
16. End the match when one faction reaches 100% map control.

## Movement

Movement is territory-to-territory for the MVP. Armies move between connected territories and always receive an ETA.

The route system must account for:

- basic land adjacency
- road speed bonuses
- rail speed bonuses
- bridge and tunnel crossings
- blocked direct water or sea crossings
- mountain and hill slowdowns
- airport-to-airport movement
- port-to-port movement

Incoming armies should be visible to affected players before arrival.

## Combat

Combat is local to the target territory. The full battle calculation should use the combat, support, and position formulas in `docs/game-rules.md`.

For MVP behavior:

- neutral territory arrival captures the territory
- enemy territory arrival starts combat
- defender receives the territory defense benefit
- attack and defense position modify battle strength
- reinforcements may join if they arrive before the battle ends
- the losing army is destroyed
- if a faction loses its final territory, it is permanently eliminated

## NPC Behavior

NPCs follow the same rules as humans. They should not cheat.

The first NPC behavior should:

- expand into nearby neutral territories
- prefer high Economy, Mobility, or Strategic Value targets
- avoid obviously losing attacks
- reinforce threatened territories
- use faster routes when available
- attack weak nearby factions after neutral expansion slows
- pursue eliminations when practical

## Frontend Experience

The application should have a simple landing page for match discovery and creation. Once a player enters a match, the active match screen should be the playable map, not a marketing page.

## Site Map

The MVP site map should be:

```text
/
  Landing page
  - primary actions: Create Game, Join Game
  - secondary action: View Created Games

/games
  Created games list
  - shows open, active, and recently completed games
  - lets players join open games
  - lets players inspect basic game settings

/games/create
  Create game
  - choose map area, default Cardiff
  - choose territory count, default 100
  - choose NPC faction count, default 6
  - choose max human players, default 2 and expandable to 4
  - create match

/games/join
  Join game
  - enter game code or select from available open games
  - choose display name/color if available
  - join lobby or match setup

/games/{gameId}/lobby
  Game lobby / setup
  - shows players currently joined
  - shows map settings
  - lets players choose starting HQ territory when setup begins
  - starts match when ready

/games/{gameId}
  Active match
  - main playable map
  - army movement
  - combat
  - leaderboard
  - realtime updates

/games/{gameId}/summary
  Match summary
  - winner
  - final leaderboard
  - eliminations
  - map-control history if available
```

## UI Design Direction

The visual style should feel like a command map: practical, readable, and focused on live territorial control.

The UI should avoid feeling like a marketing landing page once the player is inside a match. The map is the product.

Design principles:

- keep the OSM map as the main visual surface
- use colored territory overlays to show ownership
- keep panels compact and information-dense
- use clear icons/buttons for map actions
- make army movement and ETAs visible
- make the leaderboard constantly accessible
- avoid blocking the map with large modal flows during active play
- collapse side panels into bottom sheets or tabs on smaller screens

## Wireframe

### Landing Page

```text
+----------------------------------------------------------+
| Dynamic OSM World Domination                             |
|----------------------------------------------------------|
| [Create Game]  [Join Game]  [View Created Games]         |
|                                                          |
| Open games                                               |
| - Cardiff Skirmish       1/2 players    [Join]           |
| - Bay Control Test       2/4 players    [Join]           |
|                                                          |
| Recent games                                             |
| - NPC-3 won Cardiff      100% control                    |
+----------------------------------------------------------+
```

Landing page requirements:

- first viewport should immediately show the game name and match actions
- primary buttons: Create Game and Join Game
- created/open games should be visible without hunting through menus
- no large marketing hero is needed for the MVP

### Create Game

```text
+----------------------------------------------------------+
| Create Game                                              |
|----------------------------------------------------------|
| Map area             [Cardiff                  v]         |
| Territory count      [100                       ]         |
| Human players        [2                         ]         |
| NPC factions         [6                         ]         |
| Starting army        [100                       ]         |
|                                                          |
| [Create Match]                                           |
+----------------------------------------------------------+
```

Create game requirements:

- default to Cardiff
- keep MVP settings simple
- advanced map/rules settings can come later

### Join Game

```text
+----------------------------------------------------------+
| Join Game                                                |
|----------------------------------------------------------|
| Game code            [ ABC123                  ] [Join]   |
|                                                          |
| Available games                                          |
| Cardiff Skirmish     1/2 players    waiting    [Join]    |
| Cardiff 4P Test      3/4 players    waiting    [Join]    |
+----------------------------------------------------------+
```

Join game requirements:

- allow game code entry
- allow joining from open games list
- use anonymous open-play identity, not login

### Created Games List

```text
+----------------------------------------------------------+
| Created Games                                            |
|----------------------------------------------------------|
| Status    Name              Players   Map       Action   |
| Open      Cardiff Skirmish  1/2       Cardiff   [Join]   |
| Active    Bay Control       2/2       Cardiff   [View]   |
| Complete  NPC-2 Victory     -         Cardiff   [Summary]|
+----------------------------------------------------------+
```

Created games requirements:

- list open games first
- active games can be viewed or rejoined if the anonymous session matches
- completed games link to summary

### Lobby / Setup

```text
+----------------------------------------------------------+
| Cardiff Skirmish Lobby                                  |
|----------------------------------------------------------|
| Players: You, Player 2                                   |
| NPC factions: 6                                          |
| Territories: 100                                         |
|                                                          |
| Map preview                                              |
| [choose any territory as HQ]                             |
|                                                          |
| [Ready] [Start Match]                                    |
+----------------------------------------------------------+
```

Lobby requirements:

- show joined humans and NPC count
- show Cardiff map preview
- allow humans to choose any HQ territory
- show selected starts before match start

### Active Match

```text
+----------------------+-------------------------+----------------------+
| Selected Territory   | Cardiff Match           | Leaderboard          |
| Economy: 74          | OSM map + territories   | You      18% 2 elim  |
| Defense: 58          |                         | NPC-1    14% 1 elim  |
| Mobility: 82         | armies, routes, ETAs    | NPC-2     9% 0 elim  |
| Value: 77            | battles, ownership      |                      |
|                      |                         | Battle Summary       |
| Army: 100            |                         | Target: Rail Hub     |
| Route: Rail          |                         | Attack Pos: 71       |
| ETA: 01:24           |                         | Defense Pos: 64      |
| [Move] [Cancel]      |                         |                      |
+----------------------+-------------------------+----------------------+
```

Active match requirements:

The map should show:

- OSM base map
- colored territory overlays
- selected territory details
- Economy, Defense, Mobility, and Strategic Value
- armies and army strength
- route previews
- active movement ETAs
- battle status
- leaderboard
- eliminated faction state

The player should be able to:

- select a starting HQ
- select an owned army
- choose a valid destination
- preview ETA and route type
- issue movement orders
- inspect territories and leaderboard state

### Match Summary

```text
+----------------------------------------------------------+
| Match Complete                                           |
|----------------------------------------------------------|
| Winner: You                                              |
| Final map control: 100%                                  |
|                                                          |
| Final Leaderboard                                        |
| 1. You      100%   4 eliminations                        |
| 2. NPC-1      0%   eliminated by You                     |
| 3. Player 2   0%   eliminated by NPC-1                   |
+----------------------------------------------------------+
```

Match summary requirements:

- show winner
- show final leaderboard
- show elimination credits
- show eliminated-by details where available

## Testing Strategy

Automated tests should cover:

- territory stat formulas
- map-control percentage calculation by area
- NPC starting placement spacing
- movement route validation
- ETA calculation
- blocked water movement
- bridge and tunnel movement
- road, rail, airport, and port movement
- combat strength calculation
- attack and defense position formulas
- reinforcement arrival into battles
- permanent elimination
- elimination credit
- leaderboard ranking
- 100% victory detection

Backend tests should prioritize pure C# domain tests for formulas and simulation behavior before database or API tests.

## Open Decisions

These should be decided before implementation planning:

- battle duration model
- exact territory generation algorithm
- persistence requirements for match history
