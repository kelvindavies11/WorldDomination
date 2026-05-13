# Tactical Pulse Takeover Animation Design

## Goal

Improve territory takeover feedback for attacks, reinforcements, and neutral captures with Tactical Pulse animations and generated sound cues, while preserving server-authoritative gameplay and multiplayer synchronization.

## Visual Direction

Use the approved Tactical Pulse direction:

- route streak from source to target when a source exists
- troop pulse moving along the route
- target territory colour wipe / expansion clipped to the polygon
- compact impact flash on attack or capture
- softer pulse for reinforcement
- duration and intensity scale from army strength, with caps so very large armies do not block play

Small army actions should feel quick. Large army actions should feel heavier and slightly longer.

## Architecture

Gameplay state remains authoritative in the .NET API and application layer. The browser renders presentation effects only.

The API should broadcast a compact realtime animation event next to the existing `SnapshotUpdated` broadcast for player moves and start-position claims. The event describes what happened, not how the client must mutate state.

The client receives the event over SignalR, calculates Tactical Pulse presentation values, optionally plays a generated Web Audio cue, and renders MapLibre source/layer effects. The same event payload drives every connected client, so multiplayer viewers see the same source, target, action type, strength, and timing class.

## Event Contract

SignalR event name: `TerritoryActionResolved`.

Payload:

- `gameId`
- `sourceTerritoryId`
- `targetTerritoryId`
- `actionType`: `attack`, `reinforce`, or `claim`
- `strength`
- `ownerFactionId`
- `occurredAtUtc`

The existing `SnapshotUpdated` event remains the authoritative state update. `TerritoryActionResolved` is a presentation companion.

## Game Options

Add two in-game options to the existing game menu:

- Enable animations
- Enable sounds

Both default to enabled. Values persist in `localStorage`. Turning animations off skips visual effects but still applies snapshots immediately. Turning sound off keeps visuals but suppresses Web Audio.

## Testing

Add automated tests for:

- animation duration scaling from army strength
- default and persisted game option state
- options markup exposes animation and sound toggles
- multiplayer Playwright flow observes matching Tactical Pulse animation state on both player pages after the same SignalR-driven territory action

Keep existing `dotnet test Game.sln`, Node UI tests, and Playwright coverage runnable.

## Documentation

Update `README.md` and `docs/technical-architecture.md` to describe Tactical Pulse, the game options, and the realtime presentation event boundary.
