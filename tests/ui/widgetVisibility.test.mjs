import assert from "node:assert/strict";
import test from "node:test";

import { widgetToggleLabel } from "../../src/Game.Api/wwwroot/widgetVisibility.mjs";

test("widget toggle labels hide visible widgets", () => {
  assert.equal(widgetToggleLabel(new Set(), "leaderboard", "Leaderboard"), "Hide Leaderboard");
  assert.equal(widgetToggleLabel(new Set(), "player-stats", "Player Stats"), "Hide Player Stats");
  assert.equal(widgetToggleLabel(new Set(), "move-order", "Move Order"), "Hide Move Order");
});

test("widget toggle labels show hidden widgets", () => {
  assert.equal(widgetToggleLabel(new Set(["leaderboard"]), "leaderboard", "Leaderboard"), "Show Leaderboard");
  assert.equal(widgetToggleLabel(new Set(["player-stats"]), "player-stats", "Player Stats"), "Show Player Stats");
  assert.equal(widgetToggleLabel(new Set(["move-order"]), "move-order", "Move Order"), "Show Move Order");
});
