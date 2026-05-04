import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const styles = readFileSync("src/Game.Api/wwwroot/styles.css", "utf8");

test("selected territory widget uses compact sizing", () => {
  assert.match(styles, /\.selected-territory-widget\s*{\s*left: 14px;\s*top: 62px;\s*width: min\(150px, calc\(100vw - 28px\)\);/s);
  assert.match(styles, /\.selected-territory-widget \.widget-body\s*{\s*gap: 8px;/s);
  assert.match(styles, /\.selected-territory-widget \.stat-list\s*{\s*grid-template-columns: 1fr;/s);
});
