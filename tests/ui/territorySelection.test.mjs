import assert from "node:assert/strict";
import test from "node:test";

import { applyTerritorySelection } from "../../src/Game.Api/wwwroot/territorySelection.mjs";

test("selecting neutral territory clears expansion source and target state", () => {
  const state = {
    selectedTerritoryId: "human-source",
    selectedSourceTerritoryId: "human-source",
    selectedTargetTerritoryId: "neutral-old",
    selectedMovementStrength: 20,
    movementError: "old error",
    matchSnapshot: {
      territories: [
        { id: "human-source", ownerFactionId: "human-1" },
        { id: "neutral-new", ownerFactionId: null }
      ],
      armies: [
        { territoryId: "human-source", factionId: "human-1", strength: 80 }
      ]
    }
  };

  applyTerritorySelection(state, "neutral-new", []);

  assert.equal(state.selectedTerritoryId, "neutral-new");
  assert.equal(state.selectedSourceTerritoryId, null);
  assert.equal(state.selectedTargetTerritoryId, null);
  assert.equal(state.selectedMovementStrength, 1);
  assert.equal(state.movementError, null);
});

test("selecting valid neutral territory keeps source and sets expansion target", () => {
  const state = {
    selectedTerritoryId: "human-source",
    selectedSourceTerritoryId: "human-source",
    selectedTargetTerritoryId: null,
    selectedMovementStrength: 20,
    movementError: "old error",
    matchSnapshot: {
      territories: [
        { id: "human-source", ownerFactionId: "human-1" },
        { id: "neutral-target", ownerFactionId: null }
      ],
      armies: [
        { territoryId: "human-source", factionId: "human-1", strength: 80 }
      ]
    }
  };

  applyTerritorySelection(state, "neutral-target", ["neutral-target"]);

  assert.equal(state.selectedTerritoryId, "neutral-target");
  assert.equal(state.selectedSourceTerritoryId, "human-source");
  assert.equal(state.selectedTargetTerritoryId, "neutral-target");
  assert.equal(state.selectedMovementStrength, 20);
  assert.equal(state.movementError, null);
});

test("selecting player territory starts expansion planning from that territory", () => {
  const state = {
    selectedTerritoryId: null,
    selectedSourceTerritoryId: null,
    selectedTargetTerritoryId: null,
    selectedMovementStrength: 1,
    movementError: null,
    matchSnapshot: {
      territories: [
        { id: "human-source", ownerFactionId: "human-1" }
      ],
      armies: [
        { territoryId: "human-source", factionId: "human-1", strength: 81 }
      ]
    }
  };

  applyTerritorySelection(state, "human-source", []);

  assert.equal(state.selectedTerritoryId, "human-source");
  assert.equal(state.selectedSourceTerritoryId, "human-source");
  assert.equal(state.selectedTargetTerritoryId, null);
  assert.equal(state.selectedMovementStrength, 40);
});
