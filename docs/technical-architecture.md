# Technical Architecture

This document describes the technical architecture for the Dynamic OSM World Domination game.

The canonical game rules are in `docs/game-rules.md`. Technical implementation should preserve those rules and keep the rules engine independent from web, database, and frontend frameworks.

## Architecture Style

The backend should follow clean architecture.

Domain rules must not depend on ASP.NET Core, EF Core, MySQL, SignalR, FastAPI, or frontend concerns.

Dependency direction:

```text
Game.Api -> Game.Application -> Game.Domain
Game.Infrastructure -> Game.Application
Game.Infrastructure -> Game.Domain
Game.Domain -> no project dependencies
```

## Backend Projects

Recommended .NET solution projects:

- `Game.Domain`
- `Game.Application`
- `Game.Infrastructure`
- `Game.Api`
- `Game.Tests`

### Game.Domain

Owns pure game concepts and rules:

- entities
- value objects
- domain services
- formulas
- combat rules
- movement rules
- territory stat rules
- victory rules
- domain events

This project must have no dependency on ASP.NET Core, EF Core, MySQL, SignalR, FastAPI, or frontend code.

### Game.Application

Owns use cases and orchestration:

- commands
- queries
- DTOs
- application services
- simulation tick coordination
- NPC decision orchestration
- interfaces for persistence, spatial operations, OSM feature lookup, realtime notifications, time, and randomness

### Game.Infrastructure

Owns external adapters:

- EF Core persistence
- MySQL access
- spatial query adapters
- OSM ingestion adapters
- repositories
- external services
- clock and random implementations

Provider-specific MySQL spatial behavior should stay here.

### Game.Api

Owns ASP.NET Core delivery:

- Minimal API endpoints
- SignalR hubs
- anonymous player session handling
- request/response mapping
- authoritative map and game data response DTOs
- dependency injection composition
- API documentation

Map data and game data must always be returned from the API layer. The frontend must not own or store canonical map geometry, territory data, match state, rules data, or game progress outside API responses and realtime server events.

### Game.Tests

Owns automated tests:

- domain formula tests
- combat tests
- movement tests
- application use-case tests
- infrastructure integration tests
- API tests

## Core Stack

Backend:

- .NET 10 LTS
- ASP.NET Core 10
- ASP.NET Core Minimal APIs
- ASP.NET Core SignalR
- Entity Framework Core
- MySQL provider for EF Core
- MySQL 8.4 LTS or newer
- MySQL spatial data types, spatial indexes, SRIDs, and `ST_*` functions
- NetTopologySuite in C# where useful for geometry calculations

Frontend:

- React 19
- TypeScript
- Vite
- MapLibre GL JS
- SignalR TypeScript client
- TanStack Query
- Tailwind CSS v4 or CSS modules, to be decided when scaffolding

Optional gateway:

- Python FastAPI as an edge API/BFF layer when useful
- FastAPI modular routers for larger application structure
- FastAPI WebSockets only for match or auxiliary realtime flows

## FastAPI Boundary

FastAPI may be used as an outer adapter or BFF layer.

FastAPI can expose:

- client-facing aggregation endpoints
- admin/debug endpoints
- match endpoints
- AI or analytics endpoints
- lightweight orchestration endpoints

FastAPI must not own:

- authoritative match state
- combat resolution
- movement validation
- NPC behavior
- eliminations
- victory checks
- canonical game formulas

The authoritative game system remains the .NET application/domain backend.

Default responsibility split:

```text
React/Vite client
  -> FastAPI gateway/BFF for optional aggregation or auxiliary APIs
  -> ASP.NET Core API/SignalR for authoritative game commands and realtime match state
  -> Game.Application/Game.Domain for rules and simulation
  -> Game.Infrastructure/MySQL for persistence and spatial data
```

## Persistence

Use MySQL for persistence.

MySQL should store:

- games
- factions
- players
- territories
- territory polygons
- territory OSM feature summaries
- armies
- movement orders
- battles
- leaderboard rows
- eliminations
- match history, if enabled

Use MySQL spatial types for territory polygons and map bounds.

Use MySQL spatial indexes for spatial lookup.

Use focused MySQL `ST_*` queries for spatial operations that EF Core cannot translate cleanly.

Keep MySQL-specific SQL behind infrastructure services or repositories.

## Realtime

Use ASP.NET Core SignalR as the primary realtime game channel.

SignalR should broadcast:

- match state changes
- territory ownership changes
- army movement starts
- army movement ETA updates
- battle starts
- battle updates
- battle results
- eliminations
- leaderboard updates
- match victory

The frontend should treat SignalR updates as server-authoritative.

## Performance And Scaling Considerations

The game is realtime and multiplayer, so performance must be planned from the beginning.

### Authoritative Simulation

The server should be authoritative for all game state.

Clients should send commands, not state mutations. For example, a client sends "move army A to territory B"; the server validates the order, computes the route, starts the movement, and broadcasts the accepted movement event.

This prevents cheating and avoids state divergence between players in different locations.

### Match Partitioning

Each active match should be treated as an isolated simulation unit.

The simulation should be able to run one match independently from another. This allows future scaling by assigning different matches to different worker processes or servers.

For the MVP, active matches may run in the ASP.NET Core process. The architecture should still avoid assumptions that all matches must always run in one process.

### Simulation Tick Rate

Use a fixed or semi-fixed server tick for simulation updates.

The MVP should avoid extremely high tick rates. A strategy game with ETA-based movement does not need shooter-style updates.

Recommended starting point:

- simulation tick: 1-2 ticks per second
- leaderboard recalculation: on ownership changes, not every tick
- territory stat calculation: precomputed at map generation time
- route calculation: on movement order creation, then cached on the movement order

### Realtime Message Strategy

SignalR should broadcast events and compact state deltas rather than full match snapshots every tick.

Prefer events such as:

- movement started
- movement ETA changed meaningfully
- army arrived
- battle started
- battle updated
- territory owner changed
- faction eliminated
- leaderboard changed
- match ended

Use full state snapshots only for:

- initial match join
- reconnect recovery
- explicit resync after client desync

### Interest Management

Large matches should not broadcast every event to every player if the map becomes large.

The MVP can broadcast match-wide updates, but the design should allow future interest management.

Future interest filters may include:

- visible map viewport
- owned territories
- neighboring territories
- active battles involving the player
- incoming attacks against the player
- leaderboard and global elimination events

### Map And Territory Rendering

The frontend should avoid rendering thousands of heavy DOM elements.

Use MapLibre layers and GeoJSON/vector-style sources for territory overlays where possible.

Performance guidance:

- render territories as map layers, not individual React DOM nodes
- simplify territory polygons before sending them to the client
- send only the properties needed for current rendering
- use level-of-detail rules for labels, armies, and route details
- cluster or simplify army markers when zoomed out
- avoid recalculating map geometry in React render loops

### OSM Feature Processing

OSM feature extraction can be expensive and should not run during active gameplay unless necessary.

Recommended approach:

- ingest OSM data before match start
- summarize relevant features per generated territory
- calculate Economy, Defense, Mobility, and Strategic Value once during match setup
- persist feature summaries
- recalculate only if the map area or territory generation changes

### Spatial Query Performance

MySQL spatial queries should be limited and indexed.

Guidance:

- use spatial indexes for territory polygons and map bounds
- avoid running broad spatial scans during active simulation ticks
- precompute adjacency, movement connections, feature summaries, area, and centroid values
- cache route options between connected territories
- keep provider-specific spatial SQL behind infrastructure adapters

### Command Rate Limiting

Clients should not be able to spam movement commands or expensive route calculations.

Add rate limits at the API/application boundary.

Possible limits:

- per-player command rate
- per-army active order limit
- per-match command budget
- cooldowns for repeated invalid commands

### Reconnect And Recovery

Players may disconnect or change networks.

Support reconnect by sending a current match snapshot followed by live events.

The server should not depend on clients staying connected for movement, battle, NPC, elimination, or victory progression.

### Deployment Scaling

Future scaling should support:

- multiple ASP.NET Core API instances
- SignalR backplane or managed SignalR service if needed
- separate simulation workers for active matches
- queue-based command/event processing if one process is no longer enough
- read-optimized endpoints for match history and leaderboard views

For the MVP, prefer a simpler deployment while keeping boundaries clean enough to scale later.

The MVP does not require Docker Compose, RabbitMQ, MassTransit, Prometheus, Grafana, or OpenTelemetry. Add those tools only when local development, scaling, or production diagnostics justify them.

## Simulation Engine

The simulation engine should be independent from ASP.NET Core and EF Core.

Pure rule calculations belong in `Game.Domain`.

Simulation orchestration belongs in `Game.Application`.

The simulation should handle:

- active match ticks
- movement ETA progression
- arrivals
- neutral captures
- battle creation
- battle resolution
- reinforcement arrival
- NPC decisions
- elimination checks
- leaderboard recalculation
- victory checks

## Frontend Architecture

The active match screen should be the primary experience.

The product sitemap and text wireframes are defined in `docs/superpowers/specs/2026-05-01-dynamic-osm-world-domination-design.md`.

The frontend should render:

- OSM base map
- territory overlays
- faction colors
- army markers
- active routes
- ETA labels
- battle indicators
- selected territory details
- Economy, Defense, Mobility, and Strategic Value
- leaderboard
- eliminated faction state

The frontend may preview routes and ETAs, but the backend validates all final commands.

Frontend state split:

- TanStack Query for server state fetched over HTTP
- SignalR event handling for realtime match updates
- local React state for selected territory, selected army, hovered route, open panels, and UI-only details

The frontend must treat map data and game data as API-owned server state. It may cache API responses through TanStack Query and apply SignalR updates from the server, but it must not define, duplicate, or persist authoritative map geometry, territory data, match state, rules data, or game progress in frontend code.

## Testing Strategy

Prioritize backend domain tests first.

Required test areas:

- territory stat formulas
- movement ETA formulas
- attack and defense position formulas
- combat resolution
- support and reinforcement formulas
- blocked water movement
- bridge and tunnel movement
- road, rail, airport, and port movement
- map-control percentage by area
- NPC starting placement
- NPC target selection basics
- elimination credit
- leaderboard ranking
- 100% victory detection

Integration tests should cover:

- EF Core persistence
- MySQL spatial queries
- repository behavior
- API command validation
- SignalR event emission for key match events

Frontend/browser tests should cover:

- map renders
- territory overlays render
- army markers render
- route previews appear
- movement order flow
- leaderboard updates
- elimination state display

## Implementation Principles

- Keep game rules in domain/application code.
- Keep database-specific logic in infrastructure.
- Keep web transport logic in API or FastAPI gateway layers.
- Prefer explicit DTOs at API boundaries.
- Always return map data and game data from the API layer; do not store canonical game or map data in the frontend.
- Do not let frontend calculations become authoritative.
- Add tests for formulas before implementing dependent behavior.
- Keep MVP scope small enough to produce a playable loop before adding diplomacy, fog of war, unit types, or building systems.

## Technical Concerns, Solutions, And Tools

This section lists expected technical concerns and the default solution approach.

## Open Source Tooling Choices

Prefer open-source tools where practical. The following tools are the default choices unless a later implementation decision changes them.

| Area | Tooling | Purpose |
| --- | --- | --- |
| Backend runtime | .NET 10, ASP.NET Core | Authoritative game API, SignalR, Minimal APIs, hosted services |
| Clean architecture | plain C# projects | `Game.Domain`, `Game.Application`, `Game.Infrastructure`, `Game.Api`, `Game.Tests` |
| Realtime | ASP.NET Core SignalR | Primary live match event channel |
| Optional gateway | FastAPI | Optional BFF/admin/match/AI/analytics layer |
| Database | MySQL 8.4 LTS or newer | Game persistence and spatial data |
| EF provider | Pomelo.EntityFrameworkCore.MySql + MySqlConnector | EF Core access to MySQL using open-source .NET libraries |
| Geometry | NetTopologySuite | C# geometry calculations and spatial value objects |
| Background jobs, MVP | .NET `BackgroundService` | OSM ingestion, map generation, cleanup jobs |
| Anonymous identity | server-issued player/session ids | Open play without login or account creation |
| Rate limiting | ASP.NET Core rate limiting middleware | Command/API spam control |
| Logging | built-in `ILogger` | Basic structured application logs |
| Backend tests | xUnit or NUnit | Domain/application/API tests |
| Frontend | React 19, TypeScript, Vite | Web client |
| Map rendering | MapLibre GL JS | Open-source WebGL map rendering |
| Frontend server state | TanStack Query | API data fetching/cache |
| Frontend tests | Vitest, React Testing Library | Component and UI behavior tests |
| Browser tests | Playwright | End-to-end and map rendering checks |

Notes:

- Use ASP.NET Core built-in rate limiting before adding third-party rate-limit libraries.
- Use .NET hosted services first for background processing.
- Do not require auth for MVP open play. Use server-issued anonymous player/session ids instead.
- Keep MySQL-specific spatial logic in infrastructure adapters so the game domain is not coupled to MySQL.

Non-MVP tools to add only when needed:

| Area | Later Tooling | Add When |
| --- | --- | --- |
| Containers | Docker Compose | Local setup becomes annoying without scripted MySQL/API/frontend startup |
| Observability | OpenTelemetry | Logs are not enough to debug latency, simulation ticks, or distributed calls |
| Metrics | Prometheus | The game needs production-style metrics, alerting, or trend analysis |
| Dashboards | Grafana OSS | Metrics need shared visual dashboards |
| Structured logging | Serilog | Built-in logging is not enough for querying/formatting log events |
| Message broker | RabbitMQ | Match simulation or background work needs durable async queues |
| .NET message bus | MassTransit | RabbitMQ usage becomes complex enough to need a .NET abstraction |
| Persistent jobs | Hangfire or Quartz.NET | Hosted services are not enough for retries, schedules, dashboards, or durable jobs |
| Reverse proxy | YARP or Traefik | Deployment needs routing across multiple services |
| Integration test containers | Testcontainers for .NET | Integration tests need disposable MySQL/RabbitMQ instances |

### Security And Anti-Cheat

Concern:

Players may try to spoof movement, combat, ownership, army strength, or victory state.

Solution:

- Keep the .NET backend authoritative.
- Clients send commands, not state changes.
- Validate every command in `Game.Application`.
- Recalculate movement, ETA, combat, territory ownership, eliminations, and victory server-side.
- Add per-player command rate limits.
- Log rejected commands for abuse detection.

Tools:

- ASP.NET Core rate limiting middleware
- SignalR connection groups
- structured logging through `ILogger`
- domain/application command validators

### Anonymous Player Identity

Concern:

The game needs lightweight player identity for multiplayer sessions, reconnects, faction ownership, and leaderboard attribution without requiring accounts or login.

Solution:

- Use server-issued anonymous `PlayerId` and `SessionId` values.
- Store the current anonymous session id client-side.
- Allow players to join openly without registration.
- Link each human faction to a server-issued `PlayerId`.
- Do not trust client-supplied faction ownership.
- Reconnect by matching the server-issued session token to an active player/faction where possible.
- If a session token is lost, the player may need to rejoin as a new open player unless a future recovery flow is added.

Tools:

- ASP.NET Core session/cookie or server-issued opaque token
- SignalR connection-to-player mapping
- MySQL session/player persistence if reconnect across server restarts is needed
- browser local storage or cookie for the anonymous session token

### Realtime Reliability

Concern:

Players may disconnect, reconnect, or miss realtime events.

Solution:

- Use SignalR for primary game updates.
- Send a full match snapshot on join/reconnect.
- Send compact events after the snapshot.
- Include monotonically increasing match event sequence numbers.
- Let clients request resync if they detect a gap.

Tools:

- ASP.NET Core SignalR
- SignalR TypeScript client
- server-side event sequence tracking
- reconnect handlers in React

### Simulation Performance

Concern:

Realtime simulation can become expensive as matches, territories, armies, and NPCs increase.

Solution:

- Run active matches as isolated simulation units.
- Start with 1-2 simulation ticks per second.
- Recalculate leaderboard only on ownership changes.
- Precompute territory stats, adjacency, movement connections, and route options.
- Keep simulation code independent so it can move to worker processes later.

Tools:

- .NET hosted services for MVP simulation loops
- `TimeProvider` abstraction for testable time
- application-level simulation services
- future worker processes or queue consumers if needed

### Spatial Query Performance

Concern:

Spatial operations over OSM-derived data can be slow if run repeatedly during active matches.

Solution:

- Store territory polygons and map bounds in MySQL spatial columns.
- Add spatial indexes.
- Precompute feature summaries per territory before match start.
- Precompute adjacency and valid movement connections.
- Avoid broad spatial scans during ticks.
- Hide raw spatial SQL behind infrastructure adapters.

Tools:

- MySQL 8.4 spatial types and indexes
- MySQL `ST_*` spatial functions
- EF Core with MySQL provider
- NetTopologySuite for C# geometry calculations where useful

### OSM Data Ingestion

Concern:

OSM data is large, inconsistent, and expensive to process during gameplay.

Solution:

- Ingest and summarize OSM data before match start.
- Extract only the feature categories needed for MVP stats and movement.
- Store normalized feature summaries per territory.
- Keep raw OSM ingestion separate from gameplay simulation.

Tools:

- infrastructure OSM ingestion adapters
- MySQL persistence for feature summaries
- background jobs for ingestion/generation
- MapLibre for visual OSM map rendering

### Map Tile And OSM Licensing

Concern:

OpenStreetMap usage requires attribution, and tile providers may have usage limits.

Solution:

- Display required OSM attribution in the frontend.
- Choose a tile provider with acceptable usage terms for the deployment.
- Avoid excessive tile reloads and unnecessary map remounts.
- Cache map-related metadata where allowed.

Tools:

- MapLibre GL JS attribution controls
- configured raster/vector tile provider
- frontend map lifecycle management

### Rules And Balance Versioning

Concern:

Economy, Defense, Mobility, ETA, combat, and NPC formulas will change during balancing.

Solution:

- Store a ruleset version with each match.
- Keep formula weights in versioned configuration.
- Keep `docs/game-rules.md` aligned with default formula versions.
- Avoid hardcoding balance constants deep inside infrastructure or UI.

Tools:

- strongly typed .NET options/configuration
- versioned ruleset objects in `Game.Domain`
- migration or seed files for default rulesets
- automated tests for each ruleset version

### Database Migrations And Schema Safety

Concern:

The data model will evolve as the game grows.

Solution:

- Use migrations for every schema change.
- Keep migrations reviewable.
- Avoid destructive migrations without explicit migration plans.
- Seed only safe development/test data by default.

Tools:

- EF Core migrations
- MySQL migration history table
- integration tests against a disposable MySQL database

### Background Jobs

Concern:

OSM ingestion, territory generation, feature scoring, stale match cleanup, and match archiving should not block user requests.

Solution:

- Use background workers for heavy or recurring tasks.
- Keep job logic in application services.
- Keep infrastructure workers as thin adapters.

Tools:

- .NET `BackgroundService` for MVP
- future queue-based workers if needed
- FastAPI only for auxiliary orchestration if explicitly useful

### Observability

Concern:

Realtime bugs are hard to diagnose without logs and metrics.

Solution:

- Log command validation failures.
- Log simulation tick duration.
- Track active matches, active connections, command rate, event fanout, DB query time, battle resolution time, and reconnects.
- Add trace ids/correlation ids to commands.

Tools:

- `ILogger` structured logs
- ASP.NET Core health checks
- SignalR connection logging
- MySQL query logging in development

Later tools:

- OpenTelemetry for traces and metrics
- Prometheus for metrics collection
- Grafana OSS for dashboards
- Serilog if built-in logging is not enough

### Failure Recovery

Concern:

Servers, clients, or network connections may fail during active matches.

Solution:

- Persist enough match state to restore or resume.
- Send snapshots on reconnect.
- Use idempotent command handling where practical.
- Record movement orders, battles, ownership changes, eliminations, and match completion events.

Tools:

- MySQL persistence
- event sequence numbers
- reconnect snapshot endpoint
- SignalR reconnect handling

### Frontend Rendering Performance

Concern:

Large maps with many territories, armies, and routes can overload the browser.

Solution:

- Render territories through MapLibre sources/layers.
- Avoid rendering every territory as React DOM.
- Simplify polygons for client rendering.
- Use zoom-level detail rules for labels, armies, and route markers.
- Keep React state focused on UI selection and panels.

Tools:

- MapLibre GL JS
- GeoJSON/vector-style layers
- React memoization where useful
- browser performance profiling
- Playwright visual checks for critical map views

### Accessibility And Visual Clarity

Concern:

Faction colors, overlays, and dense map visuals can become unreadable.

Solution:

- Use colorblind-safe faction palettes.
- Use patterns, outlines, labels, or icons in addition to color.
- Keep selected territory and active route states visually distinct.
- Ensure leaderboard and stat panels remain readable on mobile and desktop.

Tools:

- accessible color palettes
- CSS variables or design tokens
- Playwright screenshot checks
- manual contrast review

### Cost Controls

Concern:

Realtime fanout, map tiles, spatial processing, and active simulations can become expensive.

Solution:

- Keep tick rate modest.
- Broadcast deltas instead of full snapshots.
- Precompute OSM and territory data.
- Clean up inactive matches.
- Avoid unnecessary tile reloads.
- Add metrics before scaling decisions.

Tools:

- SignalR event filtering/groups
- MySQL indexes and query plans
- match lifecycle cleanup jobs

Later tools:

- OpenTelemetry metrics
- Prometheus
- Grafana OSS
