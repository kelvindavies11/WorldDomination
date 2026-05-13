import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const styles = readFileSync("src/Game.Api/wwwroot/styles.css", "utf8");

test("move order widget occupies the compact left-side command slot", () => {
  assert.match(styles, /\.command-bar\s*{[^}]*bottom: 20px;[^}]*left: 50%;[^}]*min-width: min\(640px, calc\(100vw - 28px\)\);/s);
  assert.match(styles, /\.command-bar-territory\s*{[^}]*display: grid;/s);
  assert.doesNotMatch(styles, /\.selected-territory-widget\s*{/);
});
