import assert from "node:assert/strict";
import test from "node:test";

import { widgetToggleLabel } from "../../src/Game.Api/wwwroot/widgetVisibility.mjs";

test("widget toggle labels hide visible widgets", () => {
  assert.equal(widgetToggleLabel(new Set(), "leaderboard", "Leaderboard"), "Hide Leaderboard");
  assert.equal(widgetToggleLabel(new Set(), "selected-territory", "Selected Territory"), "Hide Selected Territory");
});

test("widget toggle labels show hidden widgets", () => {
  assert.equal(widgetToggleLabel(new Set(["leaderboard"]), "leaderboard", "Leaderboard"), "Show Leaderboard");
  assert.equal(widgetToggleLabel(new Set(["selected-territory"]), "selected-territory", "Selected Territory"), "Show Selected Territory");
});
