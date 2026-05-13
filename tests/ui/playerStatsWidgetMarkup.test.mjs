import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const appScript = readFileSync("src/Game.Api/wwwroot/app.js", "utf8");
const styles = readFileSync("src/Game.Api/wwwroot/styles.css", "utf8");

test("match screen declares player stats widget and game menu toggle", () => {
  assert.match(appScript, /player-stats-widget/);
  assert.match(appScript, /aria-label="Player stats"/);
  assert.match(appScript, /data-player-stats-panel/);
  assert.match(appScript, /playerStatsMarkup\(state\.matchSnapshot, "human-1"\)/);
  assert.match(appScript, /data-widget-target="player-stats"/);
  assert.match(appScript, /gameWidgetToggleLabel\("player-stats", "Player Stats"\)/);
});

test("player stats widget has stable positioning and metric grid styles", () => {
  assert.match(styles, /\.player-stats-widget/);
  assert.match(styles, /\.player-stats-grid/);
  assert.match(styles, /\.player-stat strong/);
});
