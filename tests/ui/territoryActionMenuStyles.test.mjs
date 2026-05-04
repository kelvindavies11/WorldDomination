import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const styles = readFileSync("src/Game.Api/wwwroot/styles.css", "utf8");

test("segmented territory menu keeps icons visible inside fixed quadrants", () => {
  assert.match(styles, /\.territory-action-menu\s*{[^}]*--menu-size: 108px;/s);
  assert.match(styles, /\.territory-action-slice-info\s*{[^}]*clip-path: polygon\(50% 50%, 50% 0, 100% 0, 100% 50%\);/s);
  assert.match(styles, /\.territory-action-slice-expand\s*{[^}]*clip-path: polygon\(50% 50%, 100% 50%, 100% 100%, 50% 100%\);/s);
  assert.match(styles, /\.territory-action-slice-attack\s*{[^}]*clip-path: polygon\(50% 50%, 50% 100%, 0 100%, 0 50%\);/s);
  assert.match(styles, /\.territory-action-slice-build\s*{[^}]*clip-path: polygon\(50% 50%, 0 50%, 0 0, 50% 0\);/s);
  assert.match(styles, /\.territory-action-slice-info \.territory-action-icon\s*{[^}]*left: 76%;[^}]*top: 24%;/s);
  assert.match(styles, /\.territory-action-slice-info\s*{[^}]*background: #17324a;[^}]*color: #9fd3ff;/s);
  assert.match(styles, /\.territory-action-slice-expand\s*{[^}]*background: #12382e;[^}]*color: #7cffd4;/s);
  assert.match(styles, /\.territory-action-menu-center\s*{[^}]*width: 38px;[^}]*height: 38px;/s);
  assert.match(styles, /\.territory-action-info-ring\s*{[^}]*inset: -38px;[^}]*animation: territory-info-slide-out 180ms ease-out both;/s);
  assert.match(styles, /\.territory-action-info-ring\.is-hiding\s*{[^}]*animation: territory-info-slide-in 160ms ease-in both;/s);
  assert.match(styles, /\.territory-action-info-ring::before\s*{[^}]*width: var\(--menu-size\);[^}]*height: var\(--menu-size\);[^}]*border-radius: 50%;/s);
  assert.match(styles, /@keyframes territory-info-slide-out/);
  assert.match(styles, /@keyframes territory-info-slide-in/);
  assert.match(styles, /\.territory-info-content\s*{[^}]*flex-direction: column-reverse;/s);
  assert.match(styles, /\.territory-info-slice-army\s*{[^}]*clip-path: polygon\(50% 50%, 2\.4% 65\.5%, 50% 0\);[^}]*--info-angle: 234deg;/s);
  assert.match(styles, /\.territory-info-content\s*{[^}]*transform: translate\(-50%, -50%\) rotate\(var\(--info-angle\)\) translateY\(-73px\) rotate\(calc\(var\(--info-angle\) \* -1\)\);/s);
  assert.match(styles, /\.territory-info-icon\s*{[^}]*width: 12px;[^}]*height: 12px;/s);
  assert.match(styles, /\.territory-info-slice\s*{[^}]*background: #17324a;/s);
  assert.doesNotMatch(styles, /\.territory-action-slice-expand\s*{[^}]*transform: rotate/s);
});
