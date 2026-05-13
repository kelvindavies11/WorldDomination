import assert from "node:assert/strict";
import test from "node:test";

import {
  attackImpactPaint,
  attackTrailPaint,
  captureExpansionFillPaint,
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

test("capture expansion fill paint stays clipped to the target polygon", () => {
  assert.deepEqual(captureExpansionFillPaint(), {
    "fill-color": ["get", "ownerColor"],
    "fill-opacity": [
      "interpolate",
      ["linear"],
      ["get", "progress"],
      0,
      0.12,
      0.72,
      0.48,
      1,
      0
    ],
    "fill-outline-color": ["get", "ownerColor"]
  });
});

test("attack trail paint brightens the attacker path before fading out", () => {
  assert.deepEqual(attackTrailPaint(), {
    "line-color": ["get", "attackColor"],
    "line-width": [
      "interpolate",
      ["linear"],
      ["get", "progress"],
      0,
      2,
      0.7,
      5,
      1,
      1.4
    ],
    "line-opacity": [
      "interpolate",
      ["linear"],
      ["get", "progress"],
      0,
      0.18,
      0.25,
      0.92,
      1,
      0
    ],
    "line-blur": 0.5
  });
});

test("attack impact paint flashes over the defended territory", () => {
  assert.deepEqual(attackImpactPaint(), {
    "fill-color": ["get", "impactColor"],
    "fill-opacity": [
      "interpolate",
      ["linear"],
      ["get", "progress"],
      0,
      0,
      0.18,
      0.38,
      1,
      0
    ],
    "fill-outline-color": ["get", "impactColor"]
  });
});
