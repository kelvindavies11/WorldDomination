import assert from "node:assert/strict";
import test from "node:test";

import { emptyLobbyMarkup } from "../../src/Game.Api/wwwroot/lobbyMarkup.mjs";

test("empty lobby includes a create game link", () => {
  const markup = emptyLobbyMarkup("/games/create");

  assert.match(markup, /href="\/games\/create"/);
  assert.match(markup, /data-link/);
  assert.match(markup, />Create Game</);
});
