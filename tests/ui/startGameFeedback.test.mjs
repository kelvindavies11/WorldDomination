import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const appScript = readFileSync("src/Game.Api/wwwroot/app.js", "utf8");

test("start-game failures are shown in the pregame overlay and restore the start button", () => {
  assert.match(appScript, /startGameErrorMarkup/);
  assert.match(appScript, /state\.movementError \? `<span class="map-chip error-chip">/);
  assert.match(appScript, /finally \{/);
  assert.match(appScript, /btn\.disabled = false/);
  assert.match(appScript, /btn\.textContent = "Start Game"/);
});
