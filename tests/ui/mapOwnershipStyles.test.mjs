import assert from "node:assert/strict";
import test from "node:test";

import {
  ownerColorForTerritory,
  territoryFillPaint
} from "../../src/Game.Api/wwwroot/mapOwnershipStyles.mjs";

const factions = [
  { id: "player-1", name: "Player 1", color: "#1f8a70" },
  { id: "player-2", name: "Player 2", color: "#2f6fbd" }
];

test("owned territories use the owning player's unique color", () => {
  const territory = { id: "territory-1", ownerFactionId: "player-2" };

  assert.equal(ownerColorForTerritory(territory, factions), "#2f6fbd");
});

test("neutral or unknown territories keep the neutral territory color", () => {
  assert.equal(ownerColorForTerritory({ id: "neutral", ownerFactionId: null }, factions), "#dceee8");
  assert.equal(ownerColorForTerritory({ id: "unknown", ownerFactionId: "missing" }, factions), "#dceee8");
});

test("territory fill paint styles owned polygons from feature color properties", () => {
  assert.deepEqual(territoryFillPaint(), {
    "fill-color": ["coalesce", ["get", "ownerColor"], "#dceee8"],
    "fill-opacity": ["case", ["!=", ["get", "ownerFactionId"], null], 0.5, 0.22]
  });
});
