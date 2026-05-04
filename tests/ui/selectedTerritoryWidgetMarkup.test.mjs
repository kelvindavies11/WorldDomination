import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const appScript = readFileSync("src/Game.Api/wwwroot/app.js", "utf8");

test("selected territory widget does not render stats or army cards", () => {
  assert.doesNotMatch(appScript, /data-selected-stats/);
  assert.doesNotMatch(appScript, /data-selected-army/);
  assert.doesNotMatch(appScript, /territoryStatsMarkup\(selectedTerritory\(\)\)/);
  assert.doesNotMatch(appScript, /selectedArmyMarkup\(selectedTerritory\(\)\)/);
});
