# Dynamic OSM World Domination Match

Backend match for a real-time OpenStreetMap world-domination strategy game.

## Current Slice

- .NET 10 clean architecture solution
- Pure domain rules for territory stats, movement ETA, combat, map control, and victory
- Deterministic Cardiff match match service
- Minimal API endpoint at `/api/matches/cardiff`
- Custom lobby flow with server-owned join, start-position selection, start-game state, player display names, and ended games
- API-authoritative snapshot model for map state and leaderboard data
- xUnit coverage for domain, application, and API behavior

## Run Tests

```powershell
dotnet test Game.sln
```

## Run API

```powershell
dotnet run --project src/Game.Api/Game.Api.csproj --urls http://localhost:5057
```

Then open:

```text
http://localhost:5057/api/matches/cardiff
```

## Run UI

The API serves the working lobby-first UI from `src/Game.Api/wwwroot`.

```powershell
dotnet run --project src/Game.Api/Game.Api.csproj --urls http://localhost:5057
```

Then open the available games list:

```text
http://localhost:5057/games
```

Current lobby behavior:

- players choose a display name before joining a custom game
- custom games stay visible in the lobby after they are ended
- the API owns lobby state, faction names, territory ownership, and leaderboard values
- realtime multiplayer synchronization should come from the API layer over SignalR; HTTP snapshot fetches are for initial load and recovery

`src/Game.Web` contains the planned Vite/React package boundary for the next frontend iteration. This environment currently has `node.exe` but no `npm`, so the checked-in working UI is dependency-free static browser code served by ASP.NET Core.
