import assert from "node:assert/strict";
import test from "node:test";

import { shouldLoadGames } from "../../src/Game.Api/wwwroot/lobbyLoading.mjs";

test("empty lobby loads once before the first games response", () => {
  assert.equal(shouldLoadGames({
    loading: false,
    gamesLoaded: false,
    gameCount: 0,
    error: null
  }), true);
});

test("empty lobby does not start another load after games have loaded", () => {
  assert.equal(shouldLoadGames({
    loading: false,
    gamesLoaded: true,
    gameCount: 0,
    error: null
  }), false);
});

test("lobby does not reload while loading or after an error is displayed", () => {
  assert.equal(shouldLoadGames({
    loading: true,
    gamesLoaded: false,
    gameCount: 0,
    error: null
  }), false);

  assert.equal(shouldLoadGames({
    loading: false,
    gamesLoaded: false,
    gameCount: 0,
    error: "The API returned HTTP 500."
  }), false);
});
