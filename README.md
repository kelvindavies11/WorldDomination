# Dynamic OSM World Domination Prototype

Backend prototype for a real-time OpenStreetMap world-domination strategy game.

## Current Slice

- .NET 10 clean architecture solution
- Pure domain rules for territory stats, movement ETA, combat, map control, and victory
- Deterministic Cardiff prototype match service
- Minimal API endpoint at `/api/prototype/cardiff`
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
http://localhost:5057/api/prototype/cardiff
```
