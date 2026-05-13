import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const appScript = readFileSync("src/Game.Api/wwwroot/app.js", "utf8");

test("selected territory details render in the playing-area chip and not as a widget", () => {
  assert.doesNotMatch(appScript, /data-selected-stats/);
  assert.doesNotMatch(appScript, /data-selected-army(?!-count)/);
  assert.doesNotMatch(appScript, /territoryStatsMarkup\(selectedTerritory\(\)\)/);
  assert.doesNotMatch(appScript, /selectedArmyMarkup\(selectedTerritory\(\)\)/);
  assert.doesNotMatch(appScript, /data-widget-id="selected-territory"/);
  assert.doesNotMatch(appScript, /selected-territory-widget/);
  assert.match(appScript, /class="command-bar/);
  assert.match(appScript, /data-movement-panel/);
  assert.match(appScript, /data-selected-name/);
  assert.match(appScript, /data-selected-army-count/);
  assert.match(appScript, /data-selected-owner/);
});
