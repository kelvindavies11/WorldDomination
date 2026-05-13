import assert from "node:assert/strict";
import test from "node:test";

import { validTargetTerritoryIds } from "../../src/Game.Api/wwwroot/matchRoutes.mjs";

const territories = [
  { id: "source", ownerFactionId: "human-1" },
  { id: "neutral-a", ownerFactionId: null },
  { id: "neutral-b", ownerFactionId: null },
  { id: "owned", ownerFactionId: "npc-1" },
  { id: "friendly", ownerFactionId: "human-1" }
];

test("valid target territories include allowed neutral and hostile routes in either route direction", () => {
  const routes = [
    { sourceTerritoryId: "source", destinationTerritoryId: "neutral-a", isAllowed: true },
    { sourceTerritoryId: "neutral-b", destinationTerritoryId: "source", isAllowed: true },
    { sourceTerritoryId: "source", destinationTerritoryId: "owned", isAllowed: true }
  ];

  assert.deepEqual(validTargetTerritoryIds({ territories, routes }, "source"), ["neutral-a", "neutral-b", "owned"]);
});

test("valid target territories hide blocked and already-friendly destinations", () => {
  const routes = [
    { sourceTerritoryId: "source", destinationTerritoryId: "neutral-a", isAllowed: false },
    { sourceTerritoryId: "source", destinationTerritoryId: "friendly", isAllowed: true }
  ];

  assert.deepEqual(validTargetTerritoryIds({ territories, routes }, "source"), []);
});
