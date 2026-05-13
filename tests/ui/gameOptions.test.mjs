import assert from "node:assert/strict";
import test from "node:test";

import {
  defaultGameOptions,
  gameOptionsMarkup,
  loadGameOptions,
  setGameOption
} from "../../src/Game.Api/wwwroot/gameOptions.mjs";

function storageStub(initial = {}) {
  const values = new Map(Object.entries(initial));
  return {
    getItem(key) {
      return values.has(key) ? values.get(key) : null;
    },
    setItem(key, value) {
      values.set(key, String(value));
    }
  };
}

test("game options default animations and sounds to enabled", () => {
  assert.deepEqual(loadGameOptions(storageStub()), defaultGameOptions);
});

test("game options load persisted disabled animation and sound values", () => {
  const options = loadGameOptions(storageStub({
    "dynamic-osm-enable-animations": "false",
    "dynamic-osm-enable-sounds": "false"
  }));

  assert.deepEqual(options, {
    enableAnimations: false,
    enableSounds: false
  });
});

test("set game option persists boolean values and returns updated options", () => {
  const storage = storageStub();

  const options = setGameOption(storage, "enableSounds", false);

  assert.equal(storage.getItem("dynamic-osm-enable-sounds"), "false");
  assert.deepEqual(options, {
    enableAnimations: true,
    enableSounds: false
  });
});

test("game options markup exposes animation and sound toggles", () => {
  const markup = gameOptionsMarkup({
    enableAnimations: false,
    enableSounds: true
  });

  assert.match(markup, /data-action="toggle-game-option"/);
  assert.match(markup, /data-option-key="enableAnimations"/);
  assert.match(markup, /data-option-key="enableSounds"/);
  assert.match(markup, /Enable animations/);
  assert.match(markup, /Enable sounds/);
  assert.match(markup, /aria-checked="false"/);
  assert.match(markup, /aria-checked="true"/);
}
);
