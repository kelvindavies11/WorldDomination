import assert from "node:assert/strict";
import test from "node:test";

import {
  applyTerritoryInfoAction,
  hideTerritoryActionMenu,
  territoryActionMenuMarkup
} from "../../src/Game.Api/wwwroot/territoryActionMenu.mjs";

test("territory action menu renders four radial action buttons at the click point", () => {
  const markup = territoryActionMenuMarkup({
    territoryId: "cf10-1",
    x: 144,
    y: 96
  });

  assert.match(markup, /data-territory-action-menu/);
  assert.match(markup, /style="--menu-x: 144px; --menu-y: 96px;"/);
  assert.match(markup, /class="territory-action-menu is-offset-right"/);
  assert.match(markup, /data-action="territory-menu-info"/);
  assert.match(markup, /data-action="territory-menu-expand"/);
  assert.match(markup, /data-action="territory-menu-attack"/);
  assert.match(markup, /data-action="territory-menu-build"/);
  assert.match(markup, /aria-label="Show territory info"/);
  assert.match(markup, /class="territory-action-wheel"/);
  assert.match(markup, /class="territory-action-slice territory-action-slice-info"/);
  assert.match(markup, /class="territory-action-slice territory-action-slice-expand"/);
  assert.match(markup, /class="territory-action-slice territory-action-slice-attack"/);
  assert.match(markup, /class="territory-action-slice territory-action-slice-build"/);
  assert.match(markup, /class="territory-action-menu-center"/);
  assert.match(markup, /<svg class="territory-action-icon"/);
  assert.doesNotMatch(markup, /territory-action-button/);
  assert.doesNotMatch(markup, />i<\/span>/);
  assert.doesNotMatch(markup, />x<\/span>/);
  assert.doesNotMatch(markup, />b<\/span>/);
});

test("territory action menu returns empty markup when no territory is selected", () => {
  assert.equal(territoryActionMenuMarkup(null), "");
  assert.equal(territoryActionMenuMarkup({ territoryId: "", x: 144, y: 96 }), "");
});

test("territory action menu renders secondary info circle", () => {
  const markup = territoryActionMenuMarkup({
    territoryId: "cf11-neutral",
    x: 144,
    y: 96,
    showInfo: true,
    info: {
      name: "Cardiff Bay",
      postcode: "CF10 5",
      owner: "Neutral territory",
      economy: 42,
      defense: 61,
      mobility: 77,
      strategicValue: 58,
      armyStrength: 12
    }
  });

  assert.match(markup, /class="territory-action-info-ring"/);
  assert.match(markup, /class="territory-info-slice territory-info-slice-economy"/);
  assert.match(markup, /class="territory-info-slice territory-info-slice-defense"/);
  assert.match(markup, /class="territory-info-slice territory-info-slice-mobility"/);
  assert.match(markup, /class="territory-info-slice territory-info-slice-value"/);
  assert.match(markup, /class="territory-info-army"/);
  assert.doesNotMatch(markup, /Cardiff Bay/);
  assert.doesNotMatch(markup, /CF10 5/);
  assert.doesNotMatch(markup, /Neutral territory/);
  assert.doesNotMatch(markup, /Econ/);
  assert.doesNotMatch(markup, /Def/);
  assert.doesNotMatch(markup, /Mob/);
  assert.doesNotMatch(markup, /Strat/);
  assert.match(markup, /data-info-icon="economy"/);
  assert.match(markup, /data-info-icon="army"/);
  assert.match(markup, /42/);
  assert.match(markup, /12/);
});

test("info action expands the menu info ring for clicked territory", () => {
  const state = {
    selectedTerritoryId: "cf10-player-1",
    territoryActionMenu: { territoryId: "cf11-neutral", x: 144, y: 96 },
    collapsedWidgets: new Set(["selected-territory"]),
    hiddenWidgets: new Set(["selected-territory", "leaderboard"])
  };

  applyTerritoryInfoAction(state);

  assert.equal(state.selectedTerritoryId, "cf11-neutral");
  assert.equal(state.territoryActionMenu.showInfo, true);
});

test("info action hides the menu info ring when clicked again", () => {
  const state = {
    selectedTerritoryId: "cf11-neutral",
    territoryActionMenu: { territoryId: "cf11-neutral", x: 144, y: 96, showInfo: true }
  };

  applyTerritoryInfoAction(state);

  assert.equal(state.selectedTerritoryId, "cf11-neutral");
  assert.equal(state.territoryActionMenu.showInfo, false);
});

test("map movement hides the territory action menu", () => {
  const state = {
    territoryActionMenu: { territoryId: "cf11-neutral", x: 144, y: 96, showInfo: true }
  };

  hideTerritoryActionMenu(state);

  assert.equal(state.territoryActionMenu, null);
});
