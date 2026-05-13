import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const appScript = readFileSync("src/Game.Api/wwwroot/app.js", "utf8");
const styles = readFileSync("src/Game.Api/wwwroot/styles.css", "utf8");

test("match page renders a player stats widget with dynamic match counts", () => {
  assert.match(appScript, /player-stats-widget/);
  assert.match(appScript, /data-player-stats/);
  assert.match(appScript, /data-human-count/);
  assert.match(appScript, /data-npc-count/);
  assert.match(appScript, /playerStatsMarkup\(\)/);
  assert.match(appScript, />Revenue<\/span><strong>\$\{revenue\}<\/strong>/);
  assert.match(appScript, />Growth<\/span><strong>\$\{armyGrowth\}<\/strong>/);
});

test("floating widgets expose a drag handle and player stats placement", () => {
  assert.match(appScript, /class="widget-toggle"/);
  assert.doesNotMatch(appScript, />Drag</);
  assert.match(appScript, /player-stats-widget/);
  assert.match(styles, /\.widget-toggle\s*\{[^}]*cursor: grab;/s);
  assert.match(styles, /\.player-stats-widget\s*\{[^}]*top: 360px;/s);
});
