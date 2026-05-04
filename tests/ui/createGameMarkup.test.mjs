import assert from "node:assert/strict";
import test from "node:test";

import { createGameFormMarkup } from "../../src/Game.Api/wwwroot/createGameMarkup.mjs";

test("create game form does not show territory count controls", () => {
  const markup = createGameFormMarkup({ creating: false });

  assert.doesNotMatch(markup, /name="territoryCount"/);
  assert.doesNotMatch(markup, /for="territoryCount"/);
  assert.doesNotMatch(markup, />Territories</);
});
