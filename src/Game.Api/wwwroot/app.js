import { captureExpansionFillPaint, ownerColorForTerritory, territoryFillPaint } from "./mapOwnershipStyles.mjs";
import { createGameFormMarkup, defaultTerritoryCount } from "./createGameMarkup.mjs";
import { shouldLoadGames } from "./lobbyLoading.mjs";
import { emptyLobbyMarkup } from "./lobbyMarkup.mjs";
import { routeBetween as findRouteBetween, validTargetTerritoryIds as findValidTargetTerritoryIds } from "./matchRoutes.mjs";
import { applyTerritoryInfoAction, hideTerritoryActionMenu, territoryActionMenuMarkup } from "./territoryActionMenu.mjs";
import { applyTerritorySelection } from "./territorySelection.mjs";
import { widgetHiddenClass, widgetToggleLabel } from "./widgetVisibility.mjs";

const app = document.querySelector("#app");

const state = {
  games: [],
  gamesLoaded: false,
  loading: false,
  error: null,
  creating: false,
  createError: null,
  matchSnapshot: null,
  matchSnapshotGameId: null,
  matchLoading: false,
  matchError: null,
  selectedTerritoryId: null,
  selectedSourceTerritoryId: null,
  selectedTargetTerritoryId: null,
  selectedMovementStrength: 1,
  movementSubmitting: false,
  movementError: null,
  territoryActionMenu: null,
  collapsedWidgets: new Set(["game-menu"]),
  hiddenWidgets: new Set(["leaderboard"])
};

let activeMap = null;
let mapInitializedForPath = null;
let hoveredTerritoryId = null;
let captureExpansionAnimationFrame = null;
let territoryInfoHideTimeout = null;

const FALLBACK_MAP_VIEW = {
  name: "Cardiff",
  center: [-3.1791, 51.4816],
  cameraBounds: [
    [-3.3300, 51.4050],
    [-3.0300, 51.5550]
  ],
  boundaryCoordinates: []
};

const routes = {
  home: "/",
  games: "/games",
  create: "/games/create",
  match: "/games/cardiff"
};

function shell(content) {
  return `
    <div class="shell">
      <header class="topbar">
        <div class="topbar-inner">
          <a class="brand" href="${routes.games}" data-link>Dynamic OSM World Domination</a>
          <nav class="nav" aria-label="Primary">
            <a href="${routes.games}" data-link>Available Games</a>
            <a href="${routes.create}" data-link>Create Game</a>
          </nav>
        </div>
      </header>
      <main>${content}</main>
    </div>
  `;
}

function renderGamesPage() {
  if (shouldLoadGames({
    loading: state.loading,
    gamesLoaded: state.gamesLoaded,
    gameCount: state.games.length,
    error: state.error
  })) {
    void loadGames();
  }

  return shell(`
    <div class="page-head">
      <div>
        <h1>Available games</h1>
        <p class="subtitle">Join an open match or create a new one.</p>
      </div>
      <a class="button" href="${routes.create}" data-link>Create Game</a>
    </div>
    ${state.loading ? `<div class="status">Loading available games...</div>` : ""}
    ${state.error ? `<div class="status error"><h2>Could not load games</h2><p>${escapeHtml(state.error)}</p><button data-action="retry-list">Retry</button></div>` : ""}
    ${!state.loading && !state.error ? gamesTable(state.games) : ""}
  `);
}

function gamesTable(games) {
  if (games.length === 0) {
    return emptyLobbyMarkup(routes.create);
  }

  return `
    <section class="card">
      <div class="table">
        <div class="table-row table-head">
          <span>Name</span>
          <span>Status</span>
          <span>Players</span>
          <span>Map</span>
          <span>NPCs</span>
          <span></span>
        </div>
        ${games.map(game => `
          <div class="table-row">
            <strong>${escapeHtml(game.name)}</strong>
            <span>${escapeHtml(game.status)}</span>
            <span>${game.humanPlayers}/${game.maxHumanPlayers}</span>
            <span>${escapeHtml(game.mapArea)}</span>
            <span>${game.npcFactions}</span>
            <a class="button secondary" href="${matchRoute(game)}" data-link>Join</a>
          </div>
        `).join("")}
      </div>
    </section>
  `;
}

function renderCreatePage() {
  return shell(`
    <div class="page-head">
      <div>
        <h1>Create game</h1>
        <p class="subtitle">Create an open game that appears in the available games list.</p>
      </div>
      <a class="button secondary" href="${routes.games}" data-link>Back to Games</a>
    </div>
    <section class="card">
      ${state.createError ? `<div class="status error"><p>${escapeHtml(state.createError)}</p></div>` : ""}
      ${createGameFormMarkup({ creating: state.creating })}
    </section>
  `);
}

function renderMatchPage() {
  return shell(`
    <section class="match-map-shell" aria-label="Cardiff active match command map">
      <div class="match-layout">
        <div class="map-stage">
          <div id="match-map" class="match-map" role="img" aria-label="OpenStreetMap view of Cardiff"></div>
          <div class="map-fallback" data-map-fallback>
            <strong>Loading Cardiff map</strong>
            <span>OpenStreetMap tiles will appear here when the map library is ready.</span>
          </div>
          <div class="map-overlay top-left">
            <span class="map-chip">OSM base</span>
            <span class="map-chip">Playing area</span>
            <span class="map-chip" data-match-status>${state.matchLoading ? "Syncing" : matchSnapshotTimeText()}</span>
          </div>
          <div class="map-overlay bottom-right">
            <span>Cardiff</span>
            <strong>51.4816, -3.1791</strong>
          </div>
          ${territoryActionMenuMarkup(state.territoryActionMenu)}
        </div>

        <aside class="floating-widget selected-territory-widget ${widgetCollapsedClass("selected-territory")} ${gameWidgetHiddenClass("selected-territory")}" aria-label="Selected territory">
          <button class="widget-toggle" type="button" data-action="toggle-widget" data-widget="selected-territory" aria-expanded="${!isWidgetCollapsed("selected-territory")}">
            <span>
              <span class="eyebrow">Selected territory</span>
              <strong data-selected-name>${selectedTerritory()?.name ?? "Cardiff Central"}</strong>
            </span>
            <span class="toggle-icon" aria-hidden="true">${isWidgetCollapsed("selected-territory") ? "+" : "-"}</span>
          </button>
          <div class="widget-body" data-widget-body>
            <div class="panel-section">
              <p class="muted" data-selected-postcode>${selectedTerritoryPostcodeText()}</p>
              <p class="muted" data-selected-owner>${selectedTerritoryOwnerText()}</p>
            </div>

            <div class="panel-section" data-movement-panel>
              ${movementPanelMarkup()}
            </div>
          </div>
        </aside>

        <aside class="floating-widget leaderboard-widget ${widgetCollapsedClass("leaderboard")} ${gameWidgetHiddenClass("leaderboard")}" aria-label="Leaderboard and match status">
          <button class="widget-toggle" type="button" data-action="toggle-widget" data-widget="leaderboard" aria-expanded="${!isWidgetCollapsed("leaderboard")}">
            <span>
              <span class="eyebrow">Leaderboard</span>
              <strong>Match status</strong>
            </span>
            <span class="toggle-icon" aria-hidden="true">${isWidgetCollapsed("leaderboard") ? "+" : "-"}</span>
          </button>
          <div class="widget-body" data-widget-body>
            <div class="panel-section">
              <div class="leaderboard" data-leaderboard>
                ${leaderboardMarkup()}
              </div>
            </div>

            <div class="panel-section">
              <h3>Match status</h3>
              <div class="status-grid">
                <div><span>Map</span><strong data-map-area>${state.matchSnapshot?.mapArea ?? "Cardiff"}</strong></div>
                <div><span>Territories</span><strong data-territory-count>${state.matchSnapshot?.territories?.length ?? "100"}</strong></div>
                <div><span>Players</span><strong>2 human</strong></div>
                <div><span>NPCs</span><strong>6</strong></div>
                <div><span>Snapshot</span><strong data-match-generated-at>${matchSnapshotTimeText()}</strong></div>
              </div>
            </div>
          </div>
        </aside>

        <aside class="floating-widget game-menu-widget ${widgetCollapsedClass("game-menu")}" aria-label="Game menu">
          <button class="burger-toggle" type="button" data-action="toggle-widget" data-widget="game-menu" aria-expanded="${!isWidgetCollapsed("game-menu")}" aria-label="Open game menu">
            <span aria-hidden="true"></span>
            <span aria-hidden="true"></span>
            <span aria-hidden="true"></span>
          </button>
          <div class="widget-body" data-widget-body>
            <div class="actions menu-actions">
              <button type="button" class="secondary" data-action="toggle-game-widget" data-widget-target="leaderboard">${gameWidgetToggleLabel("leaderboard", "Leaderboard")}</button>
              <button type="button" class="secondary" data-action="toggle-game-widget" data-widget-target="selected-territory">${gameWidgetToggleLabel("selected-territory", "Selected Territory")}</button>
              <button type="button" class="secondary" data-action="back-to-lobby">Back to Lobby</button>
              <button type="button" class="danger" data-action="end-game">End Game</button>
            </div>
          </div>
        </aside>
      </div>
    </section>
  `);
}

function isWidgetCollapsed(widget) {
  return state.collapsedWidgets.has(widget);
}

function widgetCollapsedClass(widget) {
  return isWidgetCollapsed(widget) ? "is-collapsed" : "";
}

function gameWidgetHiddenClass(widget) {
  return widgetHiddenClass(state.hiddenWidgets, widget);
}

function gameWidgetToggleLabel(widget, label) {
  return widgetToggleLabel(state.hiddenWidgets, widget, label);
}

function toggleWidget(widget) {
  if (state.collapsedWidgets.has(widget)) {
    state.collapsedWidgets.delete(widget);
  } else {
    state.collapsedWidgets.add(widget);
  }

  updateWidgetCollapseState(widget);
}

function updateWidgetCollapseState(widget) {
  const toggle = document.querySelector(`[data-action="toggle-widget"][data-widget="${widget}"]`);
  const widgetElement = toggle?.closest(".floating-widget");
  if (!toggle || !widgetElement) {
    return;
  }

  const collapsed = isWidgetCollapsed(widget);
  widgetElement.classList.toggle("is-collapsed", collapsed);
  toggle.setAttribute("aria-expanded", String(!collapsed));

  const icon = toggle.querySelector(".toggle-icon");
  if (icon) {
    icon.textContent = collapsed ? "+" : "-";
  }
}

function matchSummaryText() {
  if (state.matchLoading) {
    return "Loading match data for Cardiff.";
  }

  if (state.matchError) {
    return state.matchError;
  }

  if (!state.matchSnapshot) {
    return "Base map first, with command panels ready for territories, armies, and routes.";
  }

  return `${state.matchSnapshot.mapArea} match: ${state.matchSnapshot.territories.length} territories and ${state.matchSnapshot.leaderboard.length} factions.`;
}

function matchSnapshotTimeText() {
  return state.matchSnapshot?.snapshotGeneratedAtUtc ?? "Awaiting API snapshot";
}

function selectedTerritory() {
  const territories = state.matchSnapshot?.territories;
  if (!territories?.length) {
    return null;
  }

  return territories.find(territory => territory.id === state.selectedTerritoryId) ?? territories[0];
}

function selectedTerritoryPostcodeText() {
  const territory = selectedTerritory();
  return territory?.postcode
    ? `Postcode ${territory.postcode}`
    : "Postcode territory data is loading.";
}

function selectedTerritoryOwnerText() {
  const territory = selectedTerritory();
  if (!territory) {
    return "Select a territory to inspect ownership and stats.";
  }

  const owner = factionById(territory.ownerFactionId);
  return owner
    ? `Controlled by ${owner.name}`
    : "Neutral territory";
}

function armyStrengthForTerritory(territoryId, factionId) {
  if (!territoryId || !factionId || !state.matchSnapshot?.armies) {
    return 0;
  }

  return state.matchSnapshot.armies
    .filter(army => army.territoryId === territoryId && army.factionId === factionId)
    .reduce((total, army) => total + army.strength, 0);
}

function validTargetTerritoryIds(sourceId = state.selectedSourceTerritoryId) {
  return findValidTargetTerritoryIds(state.matchSnapshot, sourceId);
}

function movementPanelMarkup() {
  const source = state.matchSnapshot?.territories?.find(territory => territory.id === state.selectedSourceTerritoryId) ?? null;
  const target = state.matchSnapshot?.territories?.find(territory => territory.id === state.selectedTargetTerritoryId) ?? null;
  if (!source) {
    return `<p class="muted">Select one of your territories to plan an expansion.</p>`;
  }

  const available = armyStrengthForTerritory(source.id, source.ownerFactionId);
  const targets = validTargetTerritoryIds(source.id);
  if (available <= 0) {
    return `<p class="muted">This territory has no army available to move.</p>`;
  }

  if (!target) {
    return `
      <h3>Expand</h3>
      <p class="muted">${targets.length} neutral target${targets.length === 1 ? "" : "s"} connected. Select one on the map.</p>
      <div class="target-list" data-valid-targets>${validTargetsMarkup(targets)}</div>
      ${state.movementError ? `<p class="form-error">${escapeHtml(state.movementError)}</p>` : ""}
    `;
  }

  const route = routeBetween(source.id, target.id);
  const sliderValue = Math.min(Math.max(state.selectedMovementStrength, 1), available);
  return `
    <h3>Move order</h3>
    <div class="move-summary">
      <span>${escapeHtml(source.name)}</span>
      <strong>${escapeHtml(target.name)}</strong>
      <small>${route?.transport ?? "Road"} route - ETA ${route?.etaSeconds ?? "-"}s</small>
    </div>
    <label class="range-field" for="armyStrengthSlider">
      <span>Troops</span>
      <strong data-movement-strength>${sliderValue}</strong>
    </label>
    <input id="armyStrengthSlider" type="range" min="1" max="${available}" value="${sliderValue}" data-action="movement-strength">
    ${state.movementError ? `<p class="form-error">${escapeHtml(state.movementError)}</p>` : ""}
    <div class="actions compact">
      <button type="button" data-action="send-movement">${state.movementSubmitting ? "Sending..." : "Send"}</button>
      <button type="button" class="secondary" data-action="cancel-movement">Cancel</button>
    </div>
  `;
}

function routeBetween(sourceId, targetId) {
  return findRouteBetween(state.matchSnapshot, sourceId, targetId);
}

function returnToLobby() {
  state.matchSnapshot = null;
  state.matchSnapshotGameId = null;
  state.matchError = null;
  state.selectedTerritoryId = null;
  state.selectedSourceTerritoryId = null;
  state.selectedTargetTerritoryId = null;
  state.selectedMovementStrength = 1;
  state.territoryActionMenu = null;
  window.history.pushState({}, "", routes.games);
  render();
}

async function endCurrentGame() {
  const gameId = currentGameId();
  try {
    const response = await fetch(`/api/games/${encodeURIComponent(gameId)}`, { method: "DELETE" });
    if (!response.ok && response.status !== 404) {
      throw new Error(`The API returned HTTP ${response.status}.`);
    }

    state.games = [];
    returnToLobby();
  } catch (error) {
    state.movementError = error instanceof Error ? error.message : "The game could not be ended.";
    updateMatchDataInPlace();
  }
}

function toggleGameWidgetVisibility(widget) {
  if (widget !== "leaderboard" && widget !== "selected-territory") {
    return;
  }

  if (state.hiddenWidgets.has(widget)) {
    state.hiddenWidgets.delete(widget);
  } else {
    state.hiddenWidgets.add(widget);
  }

  const widgetElement = document.querySelector(`.${widgetClassName(widget)}`);
  if (widgetElement) {
    widgetElement.classList.toggle("is-hidden-by-menu", state.hiddenWidgets.has(widget));
  }

  const toggle = document.querySelector(`[data-action='toggle-game-widget'][data-widget-target='${widget}']`);
  if (toggle) {
    toggle.textContent = gameWidgetToggleLabel(widget, toggle.dataset.widgetLabel ?? defaultWidgetLabel(widget));
  }
}

function showTerritoryActionInfo() {
  if (state.territoryActionMenu) {
    state.territoryActionMenu.info = territoryActionMenuInfo(state.territoryActionMenu.territoryId);
  }
  applyTerritoryInfoAction(state);
  updateMatchDataInPlace();

  if (territoryInfoHideTimeout !== null) {
    clearTimeout(territoryInfoHideTimeout);
    territoryInfoHideTimeout = null;
  }

  if (state.territoryActionMenu?.infoHiding) {
    territoryInfoHideTimeout = setTimeout(() => {
      if (state.territoryActionMenu?.infoHiding) {
        state.territoryActionMenu.showInfo = false;
        state.territoryActionMenu.infoHiding = false;
        updateMatchDataInPlace();
      }
      territoryInfoHideTimeout = null;
    }, 170);
  }
}

function updateGameWidgetVisibilityState(widget) {
  const widgetElement = document.querySelector(`.${widgetClassName(widget)}`);
  if (widgetElement) {
    widgetElement.classList.toggle("is-hidden-by-menu", state.hiddenWidgets.has(widget));
  }

  const toggle = document.querySelector(`[data-action='toggle-game-widget'][data-widget-target='${widget}']`);
  if (toggle) {
    toggle.textContent = gameWidgetToggleLabel(widget, toggle.dataset.widgetLabel ?? defaultWidgetLabel(widget));
  }
}

function widgetClassName(widget) {
  return `${widget}-widget`;
}

function defaultWidgetLabel(widget) {
  return widget === "selected-territory" ? "Selected Territory" : "Leaderboard";
}

function validTargetsMarkup(targetIds) {
  if (targetIds.length === 0) {
    return `<span class="muted">No connected neutral territories available.</span>`;
  }

  return targetIds.map(id => {
    const territory = state.matchSnapshot?.territories?.find(item => item.id === id);
    return `<button type="button" class="target-pill" data-action="select-target" data-territory-id="${escapeHtml(id)}">${escapeHtml(territory?.name ?? id)}</button>`;
  }).join("");
}

function selectTerritoryForMovement(id) {
  applyTerritorySelection(state, id, validTargetTerritoryIds());
}

function territoryActionMenuInfo(id) {
  const territory = state.matchSnapshot?.territories?.find(item => item.id === id);
  if (!territory) {
    return null;
  }

  const owner = factionById(territory.ownerFactionId);
  return {
    name: territory.name,
    postcode: territory.postcode,
    owner: owner ? `Controlled by ${owner.name}` : "Neutral territory",
    economy: territory.stats?.economy,
    defense: territory.stats?.defense,
    mobility: territory.stats?.mobility,
    strategicValue: territory.stats?.strategicValue,
    armyStrength: armyStrengthForTerritory(territory.id, territory.ownerFactionId)
  };
}

function hideTerritoryMenuForMapMove() {
  if (!state.territoryActionMenu) {
    return;
  }

  if (territoryInfoHideTimeout !== null) {
    clearTimeout(territoryInfoHideTimeout);
    territoryInfoHideTimeout = null;
  }
  hideTerritoryActionMenu(state);
  updateMatchDataInPlace();
}

function refreshValidTargetLayer() {
  if (!activeMap?.getLayer("territory-valid-target-outline")) {
    return;
  }

  if (activeMap.getSource("territories")) {
    activeMap.getSource("territories").setData(territoryFeatureCollection());
  }
  if (activeMap.getLayer("territory-valid-target-shadow")) {
    activeMap.setFilter("territory-valid-target-shadow", validTargetFilter());
  }
  activeMap.setFilter("territory-valid-target-outline", validTargetFilter());
}

function validTargetFilter() {
  return ["==", ["get", "isValidExpansionTarget"], true];
}

function selectedTerritoryOutlineColorPaint() {
  return ["coalesce", ["get", "ownerColor"], "#ffffff"];
}

function territorySelectionColor() {
  return ["coalesce", ["get", "selectionColor"], ["get", "ownerColor"], "#ffffff"];
}

function selectedExpansionTargetColor() {
  const source = state.matchSnapshot?.territories?.find(territory => territory.id === state.selectedSourceTerritoryId);
  return ownerColorForTerritory({ ownerFactionId: source?.ownerFactionId ?? null }, state.matchSnapshot?.factions ?? []);
}

function leaderboardMarkup() {
  const rows = state.matchSnapshot?.leaderboard;
  const fallbackRows = [
    { factionName: "You", mapControlPercentage: 18, eliminationCount: 0, color: "#1f8a70" },
    { factionName: "Player 2", mapControlPercentage: 14, eliminationCount: 0, color: "#2f6fbd" },
    { factionName: "NPC-1", mapControlPercentage: 9, eliminationCount: 0, color: "#c58a1a" }
  ];

  return (rows?.length ? rows : fallbackRows).map(row => {
    const control = Number(row.mapControlPercentage) || 0;
    const color = row.color ?? factionById(row.factionId)?.color ?? "#1f8a70";
    return `
      <div class="leader-entry">
        <span class="leader-swatch" style="--swatch:${escapeHtml(color)}"></span>
        <strong>${escapeHtml(leaderboardDisplayName(row))}</strong>
        <span>${leaderboardControlText(control)}</span>
        <small>${row.eliminationCount} elim</small>
        <div class="bar"><div class="fill" style="--w:${control}%; --fill:${escapeHtml(color)}"></div></div>
      </div>
    `;
  }).join("");
}

function leaderboardDisplayName(row) {
  return row.factionId === "human-1" ? "Player 1 (You)" : row.factionName;
}

function leaderboardControlText(control) {
  return `${control.toFixed(1)}%`;
}

function factionById(factionId) {
  if (!factionId || !state.matchSnapshot?.factions) {
    return null;
  }

  return state.matchSnapshot.factions.find(faction => faction.id === factionId) ?? null;
}

async function loadGames() {
  state.loading = true;
  state.error = null;
  render();

  try {
    const response = await fetch("/api/games");
    if (!response.ok) {
      throw new Error(`The API returned HTTP ${response.status}.`);
    }

    state.games = await response.json();
    state.gamesLoaded = true;
  } catch (error) {
    state.error = error instanceof Error ? error.message : "Games could not be loaded.";
  } finally {
    state.loading = false;
    render();
  }
}

async function loadMatchSnapshot() {
  const gameId = currentGameId();
  if (state.matchSnapshotGameId !== gameId) {
    state.matchSnapshot = null;
    state.matchError = null;
    state.matchSnapshotGameId = gameId;
    state.selectedTerritoryId = null;
    state.selectedSourceTerritoryId = null;
    state.selectedTargetTerritoryId = null;
    state.selectedMovementStrength = 1;
  }

  if (state.matchLoading || state.matchSnapshot || state.matchError) {
    return;
  }

  state.matchLoading = true;
  updateMatchSummary();

  try {
    const response = await fetch(matchApiPath());
    if (!response.ok) {
      throw new Error(`The match API returned HTTP ${response.status}.`);
    }

    state.matchSnapshot = await response.json();
    state.selectedTerritoryId ??= state.matchSnapshot?.territories?.[0]?.id ?? null;
  } catch (error) {
    state.matchError = error instanceof Error ? error.message : "Match data could not be loaded.";
  } finally {
    state.matchLoading = false;
    if (isMatchRoute()) {
      updateMatchDataInPlace();
    }
  }
}

async function createGame(form) {
  const formData = new FormData(form);
  state.creating = true;
  state.createError = null;
  render();

  const request = {
    name: stringValue(formData, "name"),
    mapArea: stringValue(formData, "mapArea"),
    maxHumanPlayers: numberValue(formData, "maxHumanPlayers"),
    npcFactions: numberValue(formData, "npcFactions"),
    territoryCount: defaultTerritoryCount
  };

  try {
    const response = await fetch("/api/games", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    });

    if (!response.ok) {
      const problem = await response.json().catch(() => null);
      throw new Error(problem?.error ?? `The API returned HTTP ${response.status}.`);
    }

    await response.json();
    state.games = [];
    state.gamesLoaded = false;
    window.history.pushState({}, "", routes.games);
    await loadGames();
  } catch (error) {
    state.createError = error instanceof Error ? error.message : "The game could not be created.";
    render();
  } finally {
    state.creating = false;
  }
}

async function sendMovement() {
  if (!state.selectedSourceTerritoryId || !state.selectedTargetTerritoryId || state.movementSubmitting) {
    return;
  }

  const captureAnimation = captureExpansionRequest(
    state.selectedSourceTerritoryId,
    state.selectedTargetTerritoryId);
  state.movementSubmitting = true;
  state.movementError = null;
  updateMatchDataInPlace();

  try {
    const response = await fetch(`${matchApiPath()}/movements`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        playerFactionId: "human-1",
        sourceTerritoryId: state.selectedSourceTerritoryId,
        targetTerritoryId: state.selectedTargetTerritoryId,
        strength: state.selectedMovementStrength
      })
    });

    const result = await response.json().catch(() => null);
    if (!response.ok || !result?.accepted) {
      throw new Error(result?.error ?? "Movement command was rejected.");
    }

    state.matchSnapshot = result.snapshot;
    state.selectedTerritoryId = state.selectedTargetTerritoryId;
    state.selectedSourceTerritoryId = null;
    state.selectedTargetTerritoryId = null;
    state.selectedMovementStrength = 1;
    if (activeMap?.getSource("territories")) {
      activeMap.getSource("territories").setData(territoryFeatureCollection());
      activeMap.setFilter("territory-selected-outline", ["==", ["get", "id"], state.selectedTerritoryId ?? ""]);
    }
    animateTerritoryCaptureExpansion(captureAnimation);
  } catch (error) {
    state.movementError = error instanceof Error ? error.message : "Movement command was rejected.";
  } finally {
    state.movementSubmitting = false;
    updateMatchDataInPlace();
  }
}

function route() {
  const path = window.location.pathname;
  if (path === routes.create) {
    return renderCreatePage();
  }

  if (path.startsWith("/games/") && path !== "/games/create" && path !== "/games/cardiff/lobby") {
    return renderMatchPage();
  }

  return renderGamesPage();
}

function render() {
  disposeMapIfLeavingMatch();
  app.innerHTML = route();
  if (isMatchRoute()) {
    initMatchPage();
  }
}

function initMatchPage() {
  void loadMatchSnapshot().then(initMap);
}

function initMap() {
  const container = document.querySelector("#match-map");
  const fallback = document.querySelector("[data-map-fallback]");
  if (!container || mapInitializedForPath === window.location.pathname) {
    return;
  }

  if (activeMap) {
    activeMap.remove();
    activeMap = null;
  }

  if (!window.maplibregl) {
    fallback?.classList.add("is-visible");
    return;
  }

  try {
    activeMap = new window.maplibregl.Map({
      container,
      center: currentMapDetails().center,
      zoom: 11.2,
      minZoom: 10,
      maxZoom: 15.5,
      maxBounds: currentMapDetails().cameraBounds,
      pitch: 35,
      bearing: -12,
      attributionControl: true,
      style: {
        version: 8,
        sources: {
          osm: {
            type: "raster",
            tiles: ["https://tile.openstreetmap.org/{z}/{x}/{y}.png"],
            tileSize: 256,
            attribution: "&copy; OpenStreetMap contributors"
          }
        },
        layers: [
          {
            id: "osm",
            type: "raster",
            source: "osm"
          }
        ]
      }
    });
    activeMap.addControl(new window.maplibregl.NavigationControl({ visualizePitch: true }), "top-right");
    activeMap.once("load", () => {
      addPlayAreaBoundary(activeMap);
      addTerritoryLayers(activeMap);
      activeMap.fitBounds(currentMapDetails().cameraBounds, {
        padding: { top: 84, right: 52, bottom: 64, left: 52 },
        maxZoom: 11.5,
        duration: 0
      });
      fallback?.classList.remove("is-visible");
    });
    mapInitializedForPath = window.location.pathname;
  } catch (error) {
    fallback?.classList.add("is-visible");
  }
}

function addTerritoryLayers(map) {
  const territories = territoryFeatureCollection();
  if (territories.features.length === 0) {
    return;
  }

  if (map.getSource("territories")) {
    map.getSource("territories").setData(territories);
  } else {
    map.addSource("territories", {
      type: "geojson",
      data: territories
    });
  }

  if (!map.getLayer("territory-fill")) {
    map.addLayer({
      id: "territory-fill",
      type: "fill",
      source: "territories",
      paint: territoryFillPaint()
    });
  }

  if (!map.getSource("territory-capture-expansion")) {
    map.addSource("territory-capture-expansion", {
      type: "geojson",
      data: emptyFeatureCollection()
    });
  }

  if (!map.getLayer("territory-capture-expansion")) {
    map.addLayer({
      id: "territory-capture-expansion",
      type: "fill",
      source: "territory-capture-expansion",
      paint: captureExpansionFillPaint()
    });
  }

  if (!map.getLayer("territory-hover-fill")) {
    map.addLayer({
      id: "territory-hover-fill",
      type: "fill",
      source: "territories",
      filter: ["==", ["get", "id"], ""],
      paint: {
        "fill-color": "#f7fffb",
        "fill-opacity": 0.48
      }
    });
  }

  if (!map.getLayer("territory-outline")) {
    map.addLayer({
      id: "territory-outline",
      type: "line",
      source: "territories",
      paint: {
        "line-color": "#ffffff",
        "line-width": 0.8,
        "line-opacity": 0.42
      }
    });
  }

  if (!map.getLayer("territory-hover-outline")) {
    map.addLayer({
      id: "territory-hover-outline",
      type: "line",
      source: "territories",
      filter: ["==", ["get", "id"], ""],
      paint: {
        "line-color": "#ffffff",
        "line-width": 2,
        "line-opacity": 0.9
      }
    });
  }

  if (!map.getLayer("territory-valid-target-shadow")) {
    map.addLayer({
      id: "territory-valid-target-shadow",
      type: "line",
      source: "territories",
      filter: validTargetFilter(),
      paint: {
        "line-color": "#07130f",
        "line-width": 7,
        "line-opacity": 0.9
      }
    });
  }

  if (!map.getLayer("territory-valid-target-outline")) {
    map.addLayer({
      id: "territory-valid-target-outline",
      type: "line",
      source: "territories",
      filter: validTargetFilter(),
      paint: {
        "line-color": "#7cffd4",
        "line-width": 4,
        "line-opacity": 1,
        "line-dasharray": [1.2, 0.8]
      }
    });
  }

  if (!map.getLayer("territory-selected-outline")) {
    map.addLayer({
      id: "territory-selected-outline",
      type: "line",
      source: "territories",
      filter: ["==", ["get", "id"], state.selectedTerritoryId ?? ""],
      paint: {
        "line-color": territorySelectionColor(),
        "line-width": 4,
        "line-opacity": 0.95
      }
    });
  }

  if (!map.getLayer("territory-target-selection-outline")) {
    map.addLayer({
      id: "territory-target-selection-outline",
      type: "line",
      source: "territories",
      filter: ["==", ["get", "isSelectedExpansionTarget"], true],
      paint: {
        "line-color": territorySelectionColor(),
        "line-width": 5,
        "line-opacity": 0.98
      }
    });
  }

  map.on("click", "territory-fill", event => {
    const feature = event.features?.[0];
    const id = feature?.properties?.id;
    if (!id) {
      return;
    }

    selectTerritoryForMovement(id);
    state.territoryActionMenu = {
      territoryId: id,
      x: event.point?.x ?? 0,
      y: event.point?.y ?? 0,
      showInfo: false,
      info: territoryActionMenuInfo(id)
    };
    map.setFilter("territory-selected-outline", ["==", ["get", "id"], state.selectedTerritoryId ?? ""]);
    refreshValidTargetLayer();
    updateMatchDataInPlace();
  });

  map.on("dragstart", hideTerritoryMenuForMapMove);
  map.on("zoomstart", hideTerritoryMenuForMapMove);
  map.on("rotatestart", hideTerritoryMenuForMapMove);
  map.on("pitchstart", hideTerritoryMenuForMapMove);

  map.on("mousemove", "territory-fill", event => {
    const id = event.features?.[0]?.properties?.id;
    if (!id || id === hoveredTerritoryId) {
      return;
    }

    hoveredTerritoryId = id;
    map.setFilter("territory-hover-fill", ["==", ["get", "id"], id]);
    map.setFilter("territory-hover-outline", ["==", ["get", "id"], id]);
    map.getCanvas().style.cursor = "pointer";
  });

  map.on("mouseleave", "territory-fill", () => {
    hoveredTerritoryId = null;
    map.setFilter("territory-hover-fill", ["==", ["get", "id"], ""]);
    map.setFilter("territory-hover-outline", ["==", ["get", "id"], ""]);
    map.getCanvas().style.cursor = "";
  });
}

function territoryFeatureCollection() {
  const territories = state.matchSnapshot?.territories ?? [];
  const validTargets = new Set(validTargetTerritoryIds());
  const expansionTargetColor = selectedExpansionTargetColor();
  return {
    type: "FeatureCollection",
    features: territories
      .filter(territory => territory.boundaryCoordinates?.length >= 4)
      .map(territory => {
        const ownerColor = ownerColorForTerritory(territory, state.matchSnapshot?.factions ?? []);
        return {
          type: "Feature",
          properties: {
            id: territory.id,
            name: territory.name,
            postcode: territory.postcode,
            ownerFactionId: territory.ownerFactionId ?? null,
            isValidExpansionTarget: validTargets.has(territory.id),
            isSelectedExpansionTarget: territory.id === state.selectedTargetTerritoryId,
            selectionColor: territory.id === state.selectedTargetTerritoryId ? expansionTargetColor : ownerColor,
            ownerColor
          },
          geometry: {
            type: "Polygon",
            coordinates: [territory.boundaryCoordinates.map(coordinatePair)]
          }
        };
      })
  };
}

function emptyFeatureCollection() {
  return {
    type: "FeatureCollection",
    features: []
  };
}

function captureExpansionRequest(sourceTerritoryId, targetTerritoryId) {
  const territories = state.matchSnapshot?.territories ?? [];
  const source = territories.find(territory => territory.id === sourceTerritoryId);
  const target = territories.find(territory => territory.id === targetTerritoryId);
  if (!source || !target) {
    return null;
  }

  return {
    sourceCenter: territoryCenter(source),
    targetCenter: territoryCenter(target),
    targetCoordinates: target.boundaryCoordinates.map(coordinatePair),
    ownerColor: selectedExpansionTargetColor()
  };
}

function animateTerritoryCaptureExpansion(animation) {
  if (!activeMap || !animation?.sourceCenter || !animation?.targetCenter || !animation?.targetCoordinates?.length) {
    return;
  }

  const source = activeMap.getSource("territory-capture-expansion");
  if (!source) {
    return;
  }

  if (captureExpansionAnimationFrame !== null) {
    cancelAnimationFrame(captureExpansionAnimationFrame);
  }

  const startedAt = performance.now();
  const durationMs = 820;

  const step = now => {
    const progress = Math.min((now - startedAt) / durationMs, 1);
    source.setData(captureExpansionFeature(animation.targetCoordinates, animation.ownerColor, easeOutCubic(progress)));

    if (progress < 1) {
      captureExpansionAnimationFrame = requestAnimationFrame(step);
      return;
    }

    source.setData(emptyFeatureCollection());
    captureExpansionAnimationFrame = null;
  };

  captureExpansionAnimationFrame = requestAnimationFrame(step);
}

function captureExpansionFeature(targetCoordinates, ownerColor, progress) {
  return {
    type: "FeatureCollection",
    features: [
      {
        type: "Feature",
        properties: {
          ownerColor,
          progress
        },
        geometry: {
          type: "Polygon",
          coordinates: [targetCoordinates]
        }
      }
    ]
  };
}

function territoryCenter(territory) {
  const coordinates = territory.boundaryCoordinates ?? [];
  const usableCoordinates = coordinates.length > 1 &&
    coordinates[0].longitude === coordinates.at(-1).longitude &&
    coordinates[0].latitude === coordinates.at(-1).latitude
    ? coordinates.slice(0, -1)
    : coordinates;

  const total = usableCoordinates.reduce((sum, coordinate) => ({
    longitude: sum.longitude + coordinate.longitude,
    latitude: sum.latitude + coordinate.latitude
  }), { longitude: 0, latitude: 0 });

  return [
    total.longitude / usableCoordinates.length,
    total.latitude / usableCoordinates.length
  ];
}

function easeOutCubic(progress) {
  return 1 - Math.pow(1 - progress, 3);
}

function addPlayAreaBoundary(map) {
  const mapDetails = currentMapDetails();
  if (mapDetails.boundaryCoordinates.length === 0) {
    return;
  }

  const boundary = playAreaBoundaryFeature(mapDetails);
  const mask = outOfBoundsMaskFeature(mapDetails);

  if (map.getSource("out-of-bounds")) {
    map.getSource("out-of-bounds").setData(mask);
  } else {
    map.addSource("out-of-bounds", {
      type: "geojson",
      data: mask
    });

    map.addLayer({
      id: "out-of-bounds-mask",
      type: "fill",
      source: "out-of-bounds",
      paint: {
        "fill-color": "#6f7780",
        "fill-opacity": 0.52
      }
    });
  }

  if (map.getSource("play-area")) {
    map.getSource("play-area").setData(boundary);
    return;
  }

  map.addSource("play-area", {
    type: "geojson",
    data: boundary
  });

  map.addLayer({
    id: "play-area-fill",
    type: "fill",
    source: "play-area",
    paint: {
      "fill-color": "#1f8a70",
      "fill-opacity": 0.08
    }
  });

  map.addLayer({
    id: "play-area-outline",
    type: "line",
    source: "play-area",
    paint: {
      "line-color": "#14202a",
      "line-width": 3,
      "line-opacity": 0.92,
      "line-dasharray": [2, 1]
    }
  });
}

function currentMapDetails() {
  const map = state.matchSnapshot?.map;
  if (!map) {
    return FALLBACK_MAP_VIEW;
  }

  return {
    name: map.name,
    center: coordinatePair(map.center),
    cameraBounds: map.cameraBounds.map(coordinatePair),
    boundaryCoordinates: map.boundaryCoordinates.map(coordinatePair)
  };
}

function coordinatePair(coordinate) {
  return [coordinate.longitude, coordinate.latitude];
}

function playAreaBoundaryFeature(mapDetails) {
  return {
    type: "FeatureCollection",
    features: [
      {
        type: "Feature",
        properties: {
          name: `${mapDetails.name} play area`
        },
        geometry: {
          type: "Polygon",
          coordinates: [mapDetails.boundaryCoordinates]
        }
      }
    ]
  };
}

function outOfBoundsMaskFeature(mapDetails) {
  return {
    type: "FeatureCollection",
    features: [
      {
        type: "Feature",
        properties: {
          name: `${mapDetails.name} out of bounds`
        },
        geometry: {
          type: "Polygon",
          coordinates: [
            [
              [-180, -85],
              [180, -85],
              [180, 85],
              [-180, 85],
              [-180, -85]
            ],
            mapDetails.boundaryCoordinates
          ]
        }
      }
    ]
  };
}

function disposeMapIfLeavingMatch() {
  if (isMatchRoute()) {
    return;
  }

  if (activeMap) {
    activeMap.remove();
    activeMap = null;
  }
  mapInitializedForPath = null;
}

function isMatchRoute() {
  const path = window.location.pathname;
  return path.startsWith("/games/") && path !== "/games/create" && path !== "/games/cardiff/lobby";
}

function currentGameId() {
  const path = window.location.pathname;
  if (path === routes.match) {
    return "cardiff";
  }

  return decodeURIComponent(path.split("/").filter(Boolean).at(-1) ?? "cardiff");
}

function matchApiPath() {
  return `/api/matches/${encodeURIComponent(currentGameId())}`;
}

function updateMatchSummary() {
  const summary = document.querySelector("[data-match-summary]");
  if (summary) {
    summary.textContent = matchSummaryText();
  }
}

function updateMatchDataInPlace() {
  updateMatchSummary();

  const status = document.querySelector("[data-match-status]");
  if (status) {
    status.textContent = state.matchError ? "Offline" : matchSnapshotTimeText();
  }

  const selected = selectedTerritory();
  const selectedName = document.querySelector("[data-selected-name]");
  if (selectedName && selected) {
    selectedName.textContent = selected.name;
  }

  const selectedOwner = document.querySelector("[data-selected-owner]");
  if (selectedOwner) {
    selectedOwner.textContent = selectedTerritoryOwnerText();
  }

  const selectedPostcode = document.querySelector("[data-selected-postcode]");
  if (selectedPostcode) {
    selectedPostcode.textContent = selectedTerritoryPostcodeText();
  }

  const movementPanel = document.querySelector("[data-movement-panel]");
  if (movementPanel) {
    movementPanel.innerHTML = movementPanelMarkup();
  }

  const existingTerritoryActionMenu = document.querySelector("[data-territory-action-menu]");
  const territoryActionMenu = territoryActionMenuMarkup(state.territoryActionMenu);
  if (existingTerritoryActionMenu) {
    existingTerritoryActionMenu.outerHTML = territoryActionMenu;
  } else if (territoryActionMenu) {
    document.querySelector(".map-stage")?.insertAdjacentHTML("beforeend", territoryActionMenu);
  }

  refreshValidTargetLayer();

  const leaderboard = document.querySelector("[data-leaderboard]");
  if (leaderboard) {
    leaderboard.innerHTML = leaderboardMarkup();
  }

  const mapArea = document.querySelector("[data-map-area]");
  if (mapArea && state.matchSnapshot) {
    mapArea.textContent = state.matchSnapshot.mapArea;
  }

  const territoryCount = document.querySelector("[data-territory-count]");
  if (territoryCount && state.matchSnapshot) {
    territoryCount.textContent = String(state.matchSnapshot.territories.length);
  }

  const matchGeneratedAt = document.querySelector("[data-match-generated-at]");
  if (matchGeneratedAt) {
    matchGeneratedAt.textContent = matchSnapshotTimeText();
  }
}

function matchRoute(game) {
  return game.id === "cardiff-match"
    ? routes.match
    : `/games/${encodeURIComponent(game.id)}`;
}

function stringValue(formData, key) {
  return String(formData.get(key) ?? "").trim();
}

function numberValue(formData, key) {
  return Number.parseInt(String(formData.get(key) ?? "0"), 10);
}

function escapeHtml(value) {
  return String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}

app.addEventListener("click", event => {
  const target = event.target.closest("[data-link], [data-action]");
  if (!target) {
    return;
  }

  if (target.matches("[data-link]")) {
    event.preventDefault();
    window.history.pushState({}, "", target.getAttribute("href"));
    render();
    return;
  }

  if (target.dataset.action === "retry-list") {
    void loadGames();
    return;
  }

  if (target.dataset.action === "toggle-widget") {
    toggleWidget(target.dataset.widget);
    return;
  }

  if (target.dataset.action === "send-movement") {
    void sendMovement();
    return;
  }

  if (target.dataset.action === "back-to-lobby") {
    returnToLobby();
    return;
  }

  if (target.dataset.action === "end-game") {
    void endCurrentGame();
    return;
  }

  if (target.dataset.action === "toggle-game-widget") {
    toggleGameWidgetVisibility(target.dataset.widgetTarget);
    return;
  }

  if (target.dataset.action === "territory-menu-info") {
    showTerritoryActionInfo();
    return;
  }

  if (target.dataset.action === "select-target") {
    selectTerritoryForMovement(target.dataset.territoryId);
    if (activeMap?.getLayer("territory-selected-outline")) {
      activeMap.setFilter("territory-selected-outline", ["==", ["get", "id"], state.selectedTerritoryId ?? ""]);
    }
    refreshValidTargetLayer();
    updateMatchDataInPlace();
    return;
  }

  if (target.dataset.action === "cancel-movement") {
    state.selectedSourceTerritoryId = null;
    state.selectedTargetTerritoryId = null;
    state.movementError = null;
    state.territoryActionMenu = null;
    refreshValidTargetLayer();
    updateMatchDataInPlace();
  }
});

app.addEventListener("input", event => {
  const input = event.target.closest("[data-action='movement-strength']");
  if (!input) {
    return;
  }

  state.selectedMovementStrength = Number.parseInt(input.value, 10);
  const label = document.querySelector("[data-movement-strength]");
  if (label) {
    label.textContent = String(state.selectedMovementStrength);
  }
});

app.addEventListener("submit", event => {
  const form = event.target.closest("form[data-action='create-game']");
  if (!form) {
    return;
  }

  event.preventDefault();
  void createGame(form);
});

window.addEventListener("popstate", render);

render();
