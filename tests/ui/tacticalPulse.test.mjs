import assert from "node:assert/strict";
import test from "node:test";

import {
  createTacticalPulseEventState,
  tacticalPulseTiming
} from "../../src/Game.Api/wwwroot/tacticalPulse.mjs";

test("tactical pulse timing scales large army actions longer than small actions", () => {
  const small = tacticalPulseTiming("attack", 1);
  const large = tacticalPulseTiming("attack", 40);

  assert.ok(small.durationMs < large.durationMs);
  assert.equal(small.scale, "small");
  assert.equal(large.scale, "large");
});

test("tactical pulse timing caps very large army durations", () => {
  const large = tacticalPulseTiming("attack", 40);
  const huge = tacticalPulseTiming("attack", 400);

  assert.equal(huge.durationMs, large.maxDurationMs);
  assert.equal(huge.durationMs, tacticalPulseTiming("attack", 999).durationMs);
});

test("reinforcement timing is softer than attack timing", () => {
  const attack = tacticalPulseTiming("attack", 20);
  const reinforce = tacticalPulseTiming("reinforce", 20);

  assert.ok(reinforce.intensity < attack.intensity);
  assert.equal(reinforce.soundCue, "reinforce");
  assert.equal(attack.soundCue, "attack");
});

test("tactical pulse event state records shared multiplayer metadata", () => {
  const state = createTacticalPulseEventState({
    gameId: "game-1",
    sourceTerritoryId: "cf10-1",
    targetTerritoryId: "cf10-2",
    actionType: "attack",
    strength: 12,
    ownerFactionId: "human-1",
    occurredAtUtc: "2026-05-13T08:00:00Z"
  }, { enableAnimations: true, enableSounds: false });

  assert.equal(state.gameId, "game-1");
  assert.equal(state.sourceTerritoryId, "cf10-1");
  assert.equal(state.targetTerritoryId, "cf10-2");
  assert.equal(state.actionType, "attack");
  assert.equal(state.strength, 12);
  assert.equal(state.animationsEnabled, true);
  assert.equal(state.soundsEnabled, false);
  assert.ok(state.durationMs > 0);
});
