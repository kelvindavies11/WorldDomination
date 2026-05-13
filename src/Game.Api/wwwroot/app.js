import { gameOptionsMarkup, loadGameOptions, setGameOption } from "./gameOptions.mjs";
import { captureExpansionFillPaint, ownerColorForTerritory, territoryFillPaint } from "./mapOwnershipStyles.mjs";
import { createGameFormMarkup, defaultTerritoryCount } from "./createGameMarkup.mjs";
import { shouldLoadGames } from "./lobbyLoading.mjs";
import { emptyLobbyMarkup } from "./lobbyMarkup.mjs";
import { routeBetween as findRouteBetween, validTargetTerritoryIds as findValidTargetTerritoryIds, reinforceTargetIds as findReinforceTargetIds } from "./matchRoutes.mjs";
import { playerStatsTotals } from "./playerStats.mjs";
import { createTacticalPulseEventState } from "./tacticalPulse.mjs";
import { playTacticalPulseSound } from "./tacticalPulseSound.mjs";
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
  maps: [],
  mapsLoaded: false,
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
  hiddenWidgets: new Set(["leaderboard"]),
  playerId: localStorage.getItem("dynamic-osm-player-id") ?? "local-player",
  myFactionId: null,
  playerName: localStorage.getItem("playerName") ?? localStorage.getItem("dynamic-osm-player-name") ?? "",
  gameEndedData: null,
  gameEndOverlayDismissed: false,
  gamesFilter: "Open",
  gameOptions: loadGameOptions(localStorage)
};

let activeMap = null;
let mapInitializedForPath = null;
let hoveredTerritoryId = null;
let captureExpansionAnimationFrame = null;
let troopMarchAnimationFrame = null;
let territoryInfoHideTimeout = null;
let activeConnection = null;
let draggedWidget = null;
let dragOffset = { x: 0, y: 0 };

const FALLBACK_MAP_VIEW = {
  name: "Cardiff",
  center: [-3.118, 51.522],
  cameraBounds: [
    [-3.4500, 51.3400],
    [-2.8000, 51.7100]
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
    <section class="card" style="max-width:360px">
      <div class="field">
        <label for="player-name">Your player name</label>
        <div style="display:flex;gap:8px">
          <input id="player-name" name="playerName" value="${escapeHtml(state.playerName)}" placeholder="Enter your name" autocomplete="nickname">
          <button type="button" data-action="save-player-name">Save</button>
        </div>
      </div>
    </section>
    ${state.loading ? `<div class="status">Loading available games...</div>` : ""}
    ${state.error ? `<div class="status error"><h2>Could not load games</h2><p>${escapeHtml(state.error)}</p><button data-action="retry-list">Retry</button></div>` : ""}
    ${!state.loading && !state.error ? gamesTable(state.games, state.gamesFilter) : ""}
  `);
}

function gamesTable(games, filter = "all") {
  if (games.length === 0) {
    return emptyLobbyMarkup(routes.create);
  }

  const filters = ["all", "Open", "Started", "Ended"];
  const filtered = filter === "all" ? games : games.filter(g => g.status === filter);

  // Sort: Open first, then Started (most recent first), then Ended (most recent first)
  const sorted = [...filtered].sort((a, b) => {
    const rank = s => s === "Open" ? 0 : s === "Started" ? 1 : 2;
    if (rank(a.status) !== rank(b.status)) return rank(a.status) - rank(b.status);
    const aTime = a.status === "Started" ? a.startedAt : a.status === "Ended" ? a.endedAt : a.createdAt;
    const bTime = b.status === "Started" ? b.startedAt : b.status === "Ended" ? b.endedAt : b.createdAt;
    if (!aTime && !bTime) return 0;
    if (!aTime) return 1;
    if (!bTime) return -1;
    return new Date(bTime) - new Date(aTime);
  });

  return `
    <div class="filter-tabs" role="tablist">
      ${filters.map(f => `
        <button type="button" role="tab" class="filter-tab${filter === f ? " is-active" : ""}" data-action="filter-games" data-filter="${f}">
          ${f === "all" ? "All" : f}
        </button>
      `).join("")}
    </div>
    <section class="card">
      ${sorted.length === 0 ? `<p class="muted" style="padding:8px 0">No ${filter} games.</p>` : `
      <div class="table">
        <div class="table-row table-head">
          <span>Name</span>
          <span>Status</span>
          <span>Players</span>
          <span>Map</span>
          <span>Created</span>
          <span>Started</span>
          <span>Ended</span>
          <span></span>
        </div>
        ${sorted.map(game => `
          <div class="table-row">
            <strong>${escapeHtml(game.name)}</strong>
            <span>${escapeHtml(game.status)}</span>
            <span>${game.humanPlayers}/${game.maxHumanPlayers}</span>
            <span>${escapeHtml(game.mapArea)}</span>
            <span class="muted">${formatGameDate(game.createdAt)}</span>
            <span class="muted">${formatGameDate(game.startedAt)}</span>
            <span class="muted">${formatGameDate(game.endedAt)}</span>
            <a class="button secondary" href="${matchRoute(game)}" data-link>${game.status === "Ended" ? "View" : "Join"}</a>
          </div>
        `).join("")}
      </div>`}
    </section>
  `;
}

function formatGameDate(iso) {
  if (!iso) return "—";
  const d = new Date(iso);
  return d.toLocaleString(undefined, { month: "short", day: "numeric", hour: "2-digit", minute: "2-digit" });
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
      ${createGameFormMarkup({ creating: state.creating, maps: state.maps })}
    </section>
  `);
}

function renderMatchPage() {
  return shell(`
    <section class="match-map-shell" aria-label="Cardiff active match command map">
      <div class="match-layout">
        <div class="map-stage">
          <div id="match-map" class="match-map" role="img" aria-label="OpenStreetMap view of Cardiff"></div>
          <div class="map-loading" data-map-loading aria-label="Loading map" aria-live="polite">
            <span class="map-spinner" aria-hidden="true"></span>
            <span>Loading map&hellip;</span>
          </div>
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
          ${preGameOverlay()}
          ${gameEndOverlay()}
        </div>

        <aside class="command-bar ${gameWidgetHiddenClass("selected-territory")}" aria-label="Command bar">
          <div class="command-bar-territory" data-command-territory>
            <span class="command-bar-army-count" data-selected-army-count>
              <svg class="command-bar-army-icon" aria-hidden="true" viewBox="0 0 16 16" fill="currentColor"><path d="M2 2l3.5 3.5-1 1L1 3l1-1zm11 0l-1 1-3.5 3.5 1 1L14 3l-1-1zM7 9.5 5.5 11l1 1L8 10.5l1.5 1.5 1-1L9 9.5l1.5-1.5-1-1L8 8.5 6.5 7l-1 1L7 9.5zM4 14l1-1-1.5-1.5 1-1L6 12l1-1-1.5-1.5 1.5-1.5h-3L2.5 9.5l-1 1L3 12l-1 1 1 1h1zm8 0h1l-1-1 1.5-1.5-1-1-1.5 1.5-1.5-1.5-1 1 1.5 1.5L9 14l1-1 1.5 1.5 1-1L11 12l1.5-1.5-1-1L10 11l1.5 1.5L10 14h2z"/></svg>
              <span>${selectedTerritoryArmyCount()}</span>
            </span>
            <span class="command-bar-eyebrow">Territory</span>
            <strong class="command-bar-name" data-selected-name>${selectedTerritory()?.name ?? "—"}</strong>
            <span class="command-bar-sub" data-selected-owner>${selectedTerritoryOwnerText()}</span>
          </div>
          <div class="command-bar-divider" aria-hidden="true"></div>
          <div class="command-bar-actions" data-movement-panel>
            ${movementPanelMarkup()}
          </div>
        </aside>

        <aside class="floating-widget leaderboard-widget ${widgetCollapsedClass("leaderboard")} ${gameWidgetHiddenClass("leaderboard")}" aria-label="Leaderboard">
          <button class="widget-toggle" type="button" data-action="toggle-widget" data-widget="leaderboard" aria-expanded="${!isWidgetCollapsed("leaderboard")}">
            <span>
              <span class="eyebrow">Leaderboard</span>
              <strong>Rankings</strong>
            </span>
            <span class="toggle-icon" aria-hidden="true">${isWidgetCollapsed("leaderboard") ? "+" : "-"}</span>
          </button>
          <div class="widget-body" data-widget-body>
            <div class="panel-section">
              <div class="leaderboard" data-leaderboard>
                ${leaderboardMarkup()}
              </div>
            </div>
          </div>
        </aside>

        <aside class="floating-widget player-stats-widget ${widgetCollapsedClass("player-stats")} ${gameWidgetHiddenClass("player-stats")}" aria-label="Player stats">
          <button class="widget-toggle" type="button" data-action="toggle-widget" data-widget="player-stats" aria-expanded="${!isWidgetCollapsed("player-stats")}">
            <span>
              <span class="eyebrow">Player stats</span>
              <strong>Production</strong>
            </span>
            <span class="toggle-icon" aria-hidden="true">${isWidgetCollapsed("player-stats") ? "+" : "-"}</span>
          </button>
          <div class="widget-body" data-widget-body>
            <div class="panel-section" data-player-stats-panel>
              ${playerStatsMarkup()}
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
              <button type="button" class="secondary menu-action-btn" data-action="toggle-game-widget" data-widget-target="leaderboard">
                <svg aria-hidden="true" viewBox="0 0 16 16" fill="currentColor"><rect x="1" y="9" width="3" height="6" rx="1"/><rect x="6" y="5" width="3" height="10" rx="1"/><rect x="11" y="1" width="3" height="14" rx="1"/></svg>
                <span>${gameWidgetToggleLabel("leaderboard", "Leaderboard")}</span>
              </button>
              <button type="button" class="secondary menu-action-btn" data-action="toggle-game-widget" data-widget-target="selected-territory">
                <svg aria-hidden="true" viewBox="0 0 16 16" fill="currentColor"><path d="M8 1a5 5 0 1 0 0 10A5 5 0 0 0 8 1zM8 9a3 3 0 1 1 0-6 3 3 0 0 1 0 6z"/><circle cx="8" cy="6" r="1.5"/><line x1="8" y1="11" x2="8" y2="15" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/></svg>
                <span>${gameWidgetToggleLabel("selected-territory", "Territory")}</span>
              </button>
              <button type="button" class="secondary menu-action-btn" data-action="toggle-game-widget" data-widget-target="player-stats">
                <svg aria-hidden="true" viewBox="0 0 16 16" fill="currentColor"><circle cx="8" cy="4" r="3"/><path d="M2 14c0-3.31 2.69-6 6-6s6 2.69 6 6H2z"/></svg>
                <span>${gameWidgetToggleLabel("player-stats", "Player Stats")}</span>
              </button>
              <button type="button" class="secondary menu-action-btn" data-action="back-to-lobby">
                <svg aria-hidden="true" viewBox="0 0 16 16" fill="currentColor"><path d="M7 1L1 8l6 7v-4h8V5H7V1z"/></svg>
                <span>Back to Games</span>
              </button>
              <span data-game-end-btn>${gameEndButtonMarkup()}</span>
              <span data-game-start-btn style="display:contents">${gameStartButtonMarkup()}</span>
            </div>
            ${gameOptionsMarkup(state.gameOptions)}
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

function gameStartButtonMarkup() {
  if (!state.matchSnapshot || state.matchSnapshot.game?.isStarted || state.matchSnapshot.game?.isEnded) return "";
  return `<button type="button" style="width:100%" data-action="start-game-from-match">Start Game</button>`;
}

function gameEndButtonMarkup() {
  if (!state.matchSnapshot?.game?.isStarted || state.matchSnapshot?.game?.isEnded) return "";
  return `<button type="button" class="danger" style="width:100%" data-action="end-game">End Game</button>`;
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

function selectedTerritoryArmyCount() {
  const territory = selectedTerritory();
  if (!territory || !state.matchSnapshot?.armies) return "—";
  const total = state.matchSnapshot.armies
    .filter(a => a.territoryId === territory.id)
    .reduce((sum, a) => sum + a.strength, 0);
  return total > 0 ? String(total) : "—";
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

function reinforceTerritoryIds(sourceId = state.selectedSourceTerritoryId) {
  return findReinforceTargetIds(state.matchSnapshot, sourceId);
}

function movementPanelMarkup() {
  const source = state.matchSnapshot?.territories?.find(territory => territory.id === state.selectedSourceTerritoryId) ?? null;
  const target = state.matchSnapshot?.territories?.find(territory => territory.id === state.selectedTargetTerritoryId) ?? null;
  if (!source) {
    return `<p class="muted">Select one of your territories to plan an expansion.</p>`;
  }

  const available = armyStrengthForTerritory(source.id, source.ownerFactionId);
  const expandTargets = validTargetTerritoryIds(source.id);
  const reinforceTargets = reinforceTerritoryIds(source.id);

  if (available <= 0) {
    return `<p class="muted">This territory has no army available to move.</p>`;
  }

  if (!target) {
    const hasExpand = expandTargets.length > 0;
    const hasReinforce = reinforceTargets.length > 0;
    return `
      <h3>Move troops</h3>
      ${hasExpand ? `
        <p class="muted target-group-label">Expand — ${expandTargets.length} neutral target${expandTargets.length === 1 ? "" : "s"}</p>
        <div class="target-list" data-valid-targets>${validTargetsMarkup(expandTargets, "expand")}</div>
      ` : ""}
      ${hasReinforce ? `
        <p class="muted target-group-label${hasExpand ? " target-group-label-spaced" : ""}">Reinforce — ${reinforceTargets.length} owned target${reinforceTargets.length === 1 ? "" : "s"}</p>
        <div class="target-list" data-reinforce-targets>${validTargetsMarkup(reinforceTargets, "reinforce")}</div>
      ` : ""}
      ${!hasExpand && !hasReinforce ? `<p class="muted">No connected territories to move to.</p>` : ""}
      ${state.movementError ? `<p class="form-error">${escapeHtml(state.movementError)}</p>` : ""}
    `;
  }

  const isReinforcement = target.ownerFactionId === (state.myFactionId ?? "human-1");
  const sliderMax = isReinforcement ? Math.max(1, available - 1) : available;
  const sliderValue = Math.min(Math.max(state.selectedMovementStrength, 1), sliderMax);
  const route = routeBetween(source.id, target.id);
  const actionLabel = isReinforcement ? "Reinforce" : "Move order";
  const sendLabel = isReinforcement ? "Reinforce" : "Send";
  return `
    <h3>${actionLabel}</h3>
    <div class="move-summary">
      <span>${escapeHtml(source.name)}</span>
      <strong>${escapeHtml(target.name)}</strong>
      <small>${route?.transport ?? "Road"} route · ETA ${route?.etaSeconds ?? "-"}s</small>
    </div>
    <label class="range-field" for="armyStrengthSlider">
      <span>Troops</span>
      <strong data-movement-strength>${sliderValue}</strong>
    </label>
    <input id="armyStrengthSlider" type="range" min="1" max="${sliderMax}" value="${sliderValue}" data-action="movement-strength">
    ${isReinforcement ? `<p class="muted" style="font-size:0.78rem;margin-top:2px">1 troop stays behind in source</p>` : ""}
    ${state.movementError ? `<p class="form-error">${escapeHtml(state.movementError)}</p>` : ""}
    <div class="actions compact">
      <button type="button" data-action="send-movement">${state.movementSubmitting ? "Sending..." : sendLabel}</button>
      <button type="button" class="secondary" data-action="cancel-movement">Cancel</button>
    </div>
  `;
}

function routeBetween(sourceId, targetId) {
  return findRouteBetween(state.matchSnapshot, sourceId, targetId);
}

function playerStatsMarkup() {
  const snapshot = state.matchSnapshot;
  const myFactionId = state.myFactionId ?? "human-1";
  const humanCount = snapshot?.factions?.filter(f => f.kind === "Human").length ?? 0;
  const npcCount = snapshot?.factions?.filter(f => f.kind === "Npc").length ?? 0;
  const totals = playerStatsTotals(snapshot, myFactionId);
  const revenue = snapshot?.resources?.find(r => r.factionId === myFactionId)?.revenue ?? totals.revenuePerTick;
  const armyGrowth = snapshot?.leaderboard?.find(r => r.factionId === myFactionId)?.armyGrowth ?? totals.armyGrowthPerTick;

  return `
    <div class="player-stats-grid" data-player-stats>
      <p class="muted">
        <span data-human-count>${humanCount} ${humanCount === 1 ? "human" : "humans"}</span>
        &middot;
        <span data-npc-count>${npcCount} NPC${npcCount === 1 ? "" : "s"}</span>
      </p>
      <div class="player-stat" data-player-revenue>
        <span>Revenue</span><strong>${revenue}</strong>
      </div>
      <div class="player-stat" data-player-army>
        <span>Army</span><strong>${totals.armyStrength}</strong>
      </div>
      <div class="player-stat" data-player-army-growth>
        <span>Growth</span><strong>${armyGrowth}</strong>
      </div>
      <div class="player-stat" data-player-territory-value>
        <span>Territory value</span><strong>${totals.territoryValue}</strong>
      </div>
      <p class="muted">${totals.territoryCount} ${totals.territoryCount === 1 ? "territory" : "territories"}</p>
    </div>
  `;
}

function returnToGames() {
  state.matchSnapshot = null;
  state.matchSnapshotGameId = null;
  state.matchError = null;
  state.selectedTerritoryId = null;
  state.selectedSourceTerritoryId = null;
  state.selectedTargetTerritoryId = null;
  state.selectedMovementStrength = 1;
  state.territoryActionMenu = null;
  state.myFactionId = null;
  state.gamesLoaded = false;
  window.history.pushState({}, "", routes.games);
  void render();
}

async function endCurrentGame() {
  const gameId = currentGameId();
  try {
    const response = await fetch(`/api/games/${encodeURIComponent(gameId)}`, { method: "DELETE" });
    if (!response.ok && response.status !== 404) {
      throw new Error(`The API returned HTTP ${response.status}.`);
    }

    state.games = [];
    returnToGames();
  } catch (error) {
    state.movementError = error instanceof Error ? error.message : "The game could not be ended.";
    updateMatchDataInPlace();
  }
}

function playerNameHeaders() {
  const headers = {};
  if (state.playerId) {
    headers["X-Player-Id"] = state.playerId;
  }
  if (state.playerName) {
    headers["X-Player-Name"] = state.playerName;
  }
  return headers;
}

async function savePlayerNameAndJoin() {
  const gameId = currentGameId();
  if (!gameId) return;
  try {
    const response = await fetch(`/api/games/${encodeURIComponent(gameId)}/join`, {
      method: "POST",
      headers: playerNameHeaders()
    });
    if (response.ok) {
      const result = await response.json();
      state.myFactionId = result.factionId ?? state.myFactionId;
      state.playerFactionId = result.factionId ?? state.playerFactionId;
    }
  } catch {
    // non-fatal: name saved locally, join attempt best-effort
  }
}

function toggleGameWidgetVisibility(widget) {
  if (widget !== "leaderboard" && widget !== "selected-territory" && widget !== "player-stats") {
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
    const labelSpan = toggle.querySelector("span");
    const newLabel = gameWidgetToggleLabel(widget, toggle.dataset.widgetLabel ?? defaultWidgetLabel(widget));
    if (labelSpan) { labelSpan.textContent = newLabel; } else { toggle.textContent = newLabel; }
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
    const labelSpan = toggle.querySelector("span");
    const newLabel = gameWidgetToggleLabel(widget, toggle.dataset.widgetLabel ?? defaultWidgetLabel(widget));
    if (labelSpan) { labelSpan.textContent = newLabel; } else { toggle.textContent = newLabel; }
  }
}

function widgetClassName(widget) {
  if (widget === "selected-territory") return "command-bar";
  return `${widget}-widget`;
}

function defaultWidgetLabel(widget) {
  if (widget === "selected-territory") {
    return "Selected Territory";
  }

  return widget === "player-stats" ? "Player Stats" : "Leaderboard";
}

function initializeWidgetDragging() {
  const widgets = document.querySelectorAll(".floating-widget");
  widgets.forEach(widget => {
    const toggle = widget.querySelector(".widget-toggle, .burger-toggle");
    if (toggle) {
      toggle.addEventListener("mousedown", handleWidgetDragStart);
    }
  });
}

function handleWidgetDragStart(event) {
  if (event.button !== 0) return;

  const widget = event.currentTarget.closest(".floating-widget");
  if (!widget) return;

  draggedWidget = widget;
  const rect = widget.getBoundingClientRect();
  dragOffset = {
    x: event.clientX - rect.left,
    y: event.clientY - rect.top
  };

  widget.style.zIndex = 1000;
  document.addEventListener("mousemove", handleWidgetDragMove);
  document.addEventListener("mouseup", handleWidgetDragEnd);
  event.preventDefault();
}

function handleWidgetDragMove(event) {
  if (!draggedWidget) return;

  const x = event.clientX - dragOffset.x;
  const y = event.clientY - dragOffset.y;

  draggedWidget.style.left = x + "px";
  draggedWidget.style.top = y + "px";
  draggedWidget.style.right = "auto";
  draggedWidget.style.bottom = "auto";
}

function handleWidgetDragEnd() {
  if (draggedWidget) {
    draggedWidget.style.zIndex = 3;
  }

  draggedWidget = null;
  document.removeEventListener("mousemove", handleWidgetDragMove);
  document.removeEventListener("mouseup", handleWidgetDragEnd);
}

function validTargetsMarkup(targetIds, kind = "expand") {
  if (targetIds.length === 0) {
    return `<span class="muted">No connected territories available.</span>`;
  }

  return targetIds.map(id => {
    const territory = state.matchSnapshot?.territories?.find(item => item.id === id);
    const badgeClass = kind === "reinforce" ? "target-pill-reinforce" : "target-pill-expand";
    return `<button type="button" class="target-pill ${badgeClass}" data-action="select-target" data-territory-id="${escapeHtml(id)}">${escapeHtml(territory?.name ?? id)}</button>`;
  }).join("");
}

function selectTerritoryForMovement(id) {
  applyTerritorySelection(state, id, validTargetTerritoryIds(), reinforceTerritoryIds());
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

  const expandIds = validTargetTerritoryIds();
  const reinforceIds = reinforceTerritoryIds();

  const expandFilter = expandIds.length > 0
    ? ["in", ["get", "id"], ["literal", expandIds]]
    : emptyIdFilter();
  const reinforceFilter = reinforceIds.length > 0
    ? ["in", ["get", "id"], ["literal", reinforceIds]]
    : emptyIdFilter();

  if (activeMap.getLayer("territory-valid-target-shadow")) {
    activeMap.setFilter("territory-valid-target-shadow", expandFilter);
  }
  activeMap.setFilter("territory-valid-target-outline", expandFilter);

  if (activeMap.getLayer("territory-reinforce-target-shadow")) {
    activeMap.setFilter("territory-reinforce-target-shadow", reinforceFilter);
  }
  if (activeMap.getLayer("territory-reinforce-target-outline")) {
    activeMap.setFilter("territory-reinforce-target-outline", reinforceFilter);
  }

  if (activeMap.getLayer("territory-target-selection-outline")) {
    activeMap.setFilter("territory-target-selection-outline",
      ["==", ["get", "id"], state.selectedTargetTerritoryId ?? ""]);
    if (state.selectedTargetTerritoryId) {
      activeMap.setPaintProperty("territory-target-selection-outline",
        "line-color", selectedExpansionTargetColor());
    }
  }
}

function emptyIdFilter() {
  return ["==", ["get", "id"], ""];
}

function selectedTerritoryOutlineColorPaint() {
  return ["coalesce", ["get", "ownerColor"], "#ffffff"];
}

function selectedExpansionTargetColor() {
  const source = state.matchSnapshot?.territories?.find(territory => territory.id === state.selectedSourceTerritoryId);
  return ownerColorForTerritory({ ownerFactionId: source?.ownerFactionId ?? null }, state.matchSnapshot?.factions ?? []);
}

function leaderboardMarkup() {
  const rows = state.matchSnapshot?.leaderboard;
  const fallbackRows = [
    { factionName: "You", mapControlPercentage: 18, eliminationCount: 0, color: "#1f8a70", territoryCount: 0, armyStrength: 0, armyGrowth: 0, revenue: 0 },
    { factionName: "Player 2", mapControlPercentage: 14, eliminationCount: 0, color: "#2f6fbd", territoryCount: 0, armyStrength: 0, armyGrowth: 0, revenue: 0 },
    { factionName: "NPC-1", mapControlPercentage: 9, eliminationCount: 0, color: "#c58a1a", territoryCount: 0, armyStrength: 0, armyGrowth: 0, revenue: 0 }
  ];

  return (rows?.length ? rows : fallbackRows).map(row => {
    const control = Number(row.mapControlPercentage) || 0;
    const color = row.color ?? factionById(row.factionId)?.color ?? "#1f8a70";
    const revenue = row.revenue ?? 0;
    const army = row.armyStrength ?? 0;
    const armyGrowth = row.armyGrowth ?? 0;
    const territories = row.territoryCount ?? 0;
    const eliminated = row.isEliminated ?? false;
    return `
      <div class="leader-entry${eliminated ? " is-eliminated" : ""}">
        <span class="leader-swatch" style="--swatch:${escapeHtml(color)}"></span>
        <strong class="leader-name">${escapeHtml(leaderboardDisplayName(row))}</strong>
        <span class="leader-control">${leaderboardControlText(control)}</span>
        <div class="bar"><div class="fill" style="--w:${control}%; --fill:${escapeHtml(color)}"></div></div>
        <div class="leader-stats">
          <span title="Territories">🏴 ${territories}</span>
          <span title="Army strength">⚔️ ${army.toLocaleString()}<small class="leader-growth"> +${armyGrowth}</small></span>
          <span title="Revenue">💰 ${revenue}/turn</span>
          <small title="Eliminations">${row.eliminationCount} elim</small>
        </div>
      </div>
    `;
  }).join("");
}

function leaderboardDisplayName(row) {
  const isMe = row.factionId === (state.myFactionId ?? "human-1");
  if (!isMe) return row.factionName;
  const name = state.playerName?.trim() || row.factionName;
  return `${name} (You)`;
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
  if (!isMatchRoute()) render();

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
    if (!isMatchRoute()) render();
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
    state.gameEndedData = null;
    state.gameEndOverlayDismissed = false;
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
    winningControlPercentage: numberValue(formData, "winningControlPercentage"),
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

    const sourceTerritoryId = state.selectedSourceTerritoryId;
    const targetTerritoryId = state.selectedTargetTerritoryId;
    state.matchSnapshot = result.snapshot;
    preserveExpansionSelection(sourceTerritoryId, targetTerritoryId);
    state.selectedMovementStrength = 1;
    if (activeMap?.getSource("territories")) {
      activeMap.getSource("territories").setData(territoryFeatureCollection());
      activeMap.setFilter("territory-selected-outline", ["==", ["get", "id"], state.selectedTerritoryId ?? ""]);
    }
  } catch (error) {
    state.movementError = error instanceof Error ? error.message : "Movement command was rejected.";
  } finally {
    state.movementSubmitting = false;
    updateMatchDataInPlace();
  }
}

function preserveExpansionSelection(sourceTerritoryId, targetTerritoryId) {
  state.selectedTerritoryId = targetTerritoryId;
  state.selectedSourceTerritoryId = sourceTerritoryId;
  state.selectedTargetTerritoryId = targetTerritoryId;
}

async function loadMaps() {
  if (state.mapsLoaded) return;
  try {
    const response = await fetch("/api/maps");
    if (!response.ok) throw new Error(`Maps API returned HTTP ${response.status}.`);
    state.maps = await response.json();
    state.mapsLoaded = true;
    render();
  } catch {
    // non-fatal: form will show no options; user can retry by navigating away and back
  }
}

function route() {
  const path = window.location.pathname;
  if (path === routes.create) {
    if (!state.mapsLoaded) void loadMaps();
    return renderCreatePage();
  }

  if (path.startsWith("/games/") && path !== "/games/create") {
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
  const gameId = currentGameId();
  // Only join if we haven't already identified ourselves for this game
  if (!state.myFactionId) {
    void fetch(`/api/games/${encodeURIComponent(gameId)}/join`, {
      method: "POST",
      headers: playerNameHeaders()
    }).then(r => r.ok ? r.json() : null).then(result => {
      if (result?.factionId) {
        state.myFactionId = result.factionId;
        state.playerFactionId = result.factionId;
      }
    }).catch(() => {});
  }
  void loadMatchSnapshot().then(() => {
    initMap();
    void joinMatchSignalR(gameId);
  });
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
    document.querySelector("[data-map-loading]")?.remove();
    fallback?.classList.add("is-visible");
    return;
  }

  try {
    activeMap = new window.maplibregl.Map({
      container,
      center: currentMapDetails().center,
      zoom: 11.2,
      minZoom: 8,
      maxZoom: 15.5,
      maxBounds: currentMapDetails().cameraBounds,
      pitch: 0,
      bearing: 0,
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
      requestAnimationFrame(() => document.querySelector("[data-map-loading]")?.remove());
      initializeWidgetDragging();
    });
    mapInitializedForPath = window.location.pathname;
  } catch (error) {
    document.querySelector("[data-map-loading]")?.remove();
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

  if (!map.getSource("troop-march")) {
    map.addSource("troop-march", {
      type: "geojson",
      data: emptyFeatureCollection()
    });
  }

  if (!map.getLayer("troop-march-trail")) {
    map.addLayer({
      id: "troop-march-trail",
      type: "line",
      source: "troop-march",
      filter: ["==", ["geometry-type"], "LineString"],
      paint: {
        "line-color": ["coalesce", ["get", "color"], "#ffffff"],
        "line-width": 2,
        "line-opacity": 0.45,
        "line-dasharray": [3, 3]
      }
    });
  }

  if (!map.getLayer("troop-march-dot")) {
    map.addLayer({
      id: "troop-march-dot",
      type: "circle",
      source: "troop-march",
      filter: ["==", ["geometry-type"], "Point"],
      paint: {
        "circle-radius": ["interpolate", ["linear"], ["zoom"], 9, 5, 13, 10],
        "circle-color": ["coalesce", ["get", "color"], "#ffffff"],
        "circle-opacity": ["coalesce", ["get", "opacity"], 1],
        "circle-stroke-width": 2,
        "circle-stroke-color": "#000000",
        "circle-stroke-opacity": 0.55
      }
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
      filter: emptyIdFilter(),
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
      filter: emptyIdFilter(),
      paint: {
        "line-color": "#7cffd4",
        "line-width": 4,
        "line-opacity": 1,
        "line-dasharray": [1.2, 0.8]
      }
    });
  }

  if (!map.getLayer("territory-reinforce-target-shadow")) {
    map.addLayer({
      id: "territory-reinforce-target-shadow",
      type: "line",
      source: "territories",
      filter: emptyIdFilter(),
      paint: {
        "line-color": "#0a1020",
        "line-width": 7,
        "line-opacity": 0.9
      }
    });
  }

  if (!map.getLayer("territory-reinforce-target-outline")) {
    map.addLayer({
      id: "territory-reinforce-target-outline",
      type: "line",
      source: "territories",
      filter: emptyIdFilter(),
      paint: {
        "line-color": "#67a6ff",
        "line-width": 4,
        "line-opacity": 1,
        "line-dasharray": [2.5, 1.2]
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
        "line-color": ["coalesce", ["get", "ownerColor"], "#ffffff"],
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
      filter: ["==", ["get", "id"], ""],
      paint: {
        "line-color": selectedExpansionTargetColor(),
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

    if (state.matchSnapshot && !state.matchSnapshot.game?.isStarted) {
      const territory = state.matchSnapshot.territories?.find(t => t.id === id);
      if (territory && !territory.ownerFactionId) {
        void selectStartFromMap(id);
      }
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

function animateTroopMarch(sourceCenter, targetCenter, color, isReinforce, durationMs = isReinforce ? 700 : 550) {
  if (!activeMap || !sourceCenter || !targetCenter) {
    return;
  }

  const marchSource = activeMap.getSource("troop-march");
  if (!marchSource) {
    return;
  }

  if (troopMarchAnimationFrame !== null) {
    cancelAnimationFrame(troopMarchAnimationFrame);
    troopMarchAnimationFrame = null;
  }

  const startedAt = performance.now();
  const trailCoords = [sourceCenter, targetCenter];

  const step = now => {
    const raw = Math.min((now - startedAt) / durationMs, 1);
    const progress = easeInOutQuad(raw);
    const lng = sourceCenter[0] + (targetCenter[0] - sourceCenter[0]) * progress;
    const lat = sourceCenter[1] + (targetCenter[1] - sourceCenter[1]) * progress;
    const opacity = raw < 0.85 ? 1 : 1 - (raw - 0.85) / 0.15;

    marchSource.setData({
      type: "FeatureCollection",
      features: [
        {
          type: "Feature",
          properties: { color },
          geometry: { type: "LineString", coordinates: trailCoords }
        },
        {
          type: "Feature",
          properties: { color, opacity },
          geometry: { type: "Point", coordinates: [lng, lat] }
        }
      ]
    });

    if (raw < 1) {
      troopMarchAnimationFrame = requestAnimationFrame(step);
      return;
    }

    marchSource.setData(emptyFeatureCollection());
    troopMarchAnimationFrame = null;
  };

  troopMarchAnimationFrame = requestAnimationFrame(step);
}

function easeInOutQuad(t) {
  return t < 0.5 ? 2 * t * t : 1 - Math.pow(-2 * t + 2, 2) / 2;
}

function captureExpansionRequest(sourceTerritoryId, targetTerritoryId, ownerColor = selectedExpansionTargetColor()) {
  const territories = state.matchSnapshot?.territories ?? [];
  const source = territories.find(territory => territory.id === sourceTerritoryId);
  const target = territories.find(territory => territory.id === targetTerritoryId);
  if (!target || (sourceTerritoryId && !source)) {
    return null;
  }

  return {
    sourceCenter: source ? territoryCenter(source) : territoryCenter(target),
    targetCenter: territoryCenter(target),
    targetCoordinates: target.boundaryCoordinates.map(coordinatePair),
    ownerColor
  };
}

function animateTerritoryCaptureExpansion(animation, durationMs = 820) {
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

function handleTerritoryActionResolved(event) {
  if (!isMatchRoute()) return;

  const eventState = createTacticalPulseEventState(event, state.gameOptions);
  window.__lastTacticalPulseEvent__ = eventState;

  if (eventState.soundsEnabled) {
    playTacticalPulseSound(eventState);
  }

  if (!eventState.animationsEnabled) {
    return;
  }

  const ownerColor = factionById(eventState.ownerFactionId)?.color ?? selectedExpansionTargetColor();
  const animation = captureExpansionRequest(
    eventState.sourceTerritoryId,
    eventState.targetTerritoryId,
    ownerColor);

  if (!animation) {
    return;
  }

  const isReinforce = eventState.actionType === "reinforce";
  const marchColor = isReinforce ? (ownerColor ?? "#67a6ff") : (ownerColor ?? "#7cffd4");

  if (animation.sourceCenter && animation.targetCenter) {
    animateTroopMarch(animation.sourceCenter, animation.targetCenter, marchColor, isReinforce, eventState.durationMs);
  }

  if (!isReinforce) {
    animateTerritoryCaptureExpansion(animation, eventState.durationMs);
  }
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
  void leaveMatchSignalR();
  if (activeMap) {
    activeMap.remove();
    activeMap = null;
  }
  mapInitializedForPath = null;
}

function isMatchRoute() {
  const path = window.location.pathname;
  return path.startsWith("/games/") && path !== "/games/create";
}

function currentGameId() {
  const path = window.location.pathname;
  if (path === routes.match) {
    return "cardiff";
  }

  if (!path.startsWith("/games/") || path === "/games/create") {
    return null;
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
  if (selectedName) {
    selectedName.textContent = selected?.name ?? "—";
  }

  const selectedOwner = document.querySelector("[data-selected-owner]");
  if (selectedOwner) {
    selectedOwner.textContent = selectedTerritoryOwnerText();
  }

  const selectedArmyCount = document.querySelector("[data-selected-army-count]");
  if (selectedArmyCount) {
    const countSpan = selectedArmyCount.querySelector("span");
    if (countSpan) countSpan.textContent = selectedTerritoryArmyCount();
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

  const playerStats = document.querySelector("[data-player-stats-panel]");
  if (playerStats) {
    playerStats.innerHTML = playerStatsMarkup(state.matchSnapshot, "human-1");
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

  const gameStartBtn = document.querySelector("[data-game-start-btn]");
  if (gameStartBtn) {
    gameStartBtn.innerHTML = gameStartButtonMarkup();
  }

  const gameEndBtn = document.querySelector("[data-game-end-btn]");
  if (gameEndBtn) {
    gameEndBtn.innerHTML = gameEndButtonMarkup();
  }

  const preGameOverlayEl = document.querySelector("[data-pregame-overlay]");
  if (preGameOverlayEl) {
    preGameOverlayEl.outerHTML = preGameOverlay();
  }

  const gameEndOverlayEl = document.querySelector("[data-game-end-overlay]");
  if (gameEndOverlayEl) {
    gameEndOverlayEl.outerHTML = gameEndOverlay();
  }

  if (activeMap?.getSource("territories")) {
    activeMap.getSource("territories").setData(territoryFeatureCollection());
  }
}

function humanStartTerritory() {
  const snap = state.matchSnapshot;
  if (!snap) return null;
  const humanFaction = snap.factions?.find(f => f.kind === "Human");
  if (!humanFaction) return null;
  return snap.territories?.find(t => t.ownerFactionId === humanFaction.id) ?? null;
}

function preGameOverlay() {
  if (!state.matchSnapshot || state.matchSnapshot.game?.isStarted) return `<span data-pregame-overlay hidden></span>`;
  const startTerritory = humanStartTerritory();
  if (startTerritory) {
    return `<div class="map-overlay top-center" data-pregame-overlay><span class="map-chip">HQ set: ${escapeHtml(startTerritory.name)}</span>${startGameErrorMarkup()}</div>`;
  }
  return `<div class="map-overlay top-center" data-pregame-overlay><span class="map-chip">Click a territory on the map to set your HQ</span>${startGameErrorMarkup()}</div>`;
}

function startGameErrorMarkup() {
  return state.movementError ? `<span class="map-chip error-chip">${escapeHtml(state.movementError)}</span>` : "";
}

function gameEndOverlay() {
  // Show if we received a GameEnded signal, OR if the loaded snapshot says the game is ended
  const ended = state.gameEndedData;
  const snapshotEnded = state.matchSnapshot?.game?.isEnded;
  if (!ended && !snapshotEnded) return `<span data-game-end-overlay hidden></span>`;

  const winnerName = ended?.winnerFactionName ?? state.matchSnapshot?.game?.winnerFactionName;
  const winnerId = ended?.winnerFactionId;
  const isStalemate = winnerName === "Stalemate" || (!winnerId && !winnerName);
  const humanFactionId = state.matchSnapshot?.factions?.find(f => f.kind === "Human")?.id;
  const isHumanWinner = winnerId && winnerId === humanFactionId;

  if (state.gameEndOverlayDismissed) return `<span data-game-end-overlay hidden></span>`;

  let title, subtitle;
  if (isStalemate) {
    title = "Game Over";
    subtitle = "The game has ended.";
  } else if (isHumanWinner) {
    title = "Victory!";
    subtitle = `${escapeHtml(winnerName)} has reached the winning control percentage.`;
  } else {
    title = "Defeat";
    subtitle = `${escapeHtml(winnerName ?? "An opponent")} has reached the winning control percentage.`;
  }

  return `<div class="map-overlay game-end-overlay" data-game-end-overlay>
    <button class="game-end-close" type="button" data-action="dismiss-game-end" aria-label="Close">&times;</button>
    <strong class="game-end-title">${title}</strong>
    <p class="game-end-subtitle">${subtitle}</p>
    <a class="button secondary" href="/games" data-link>Back to Games</a>
  </div>`;
}

function showEliminationToast(eliminatedName, youEliminated) {
  const toast = document.createElement("div");
  toast.className = "elimination-toast";
  toast.innerHTML = youEliminated
    ? `<strong>Eliminated!</strong><span>You eliminated <em>${escapeHtml(eliminatedName)}</em></span>`
    : `<span><em>${escapeHtml(eliminatedName)}</em> has been eliminated</span>`;
  document.body.appendChild(toast);
  // Animate in then out
  requestAnimationFrame(() => {
    toast.classList.add("is-visible");
    setTimeout(() => {
      toast.classList.remove("is-visible");
      toast.addEventListener("transitionend", () => toast.remove(), { once: true });
    }, 4000);
  });
}

async function selectStartFromMap(territoryId) {
  const gameId = currentGameId();
  try {
    const response = await fetch(`/api/games/${encodeURIComponent(gameId)}/start-position`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ territoryId })
    });
    if (!response.ok) {
      const problem = await response.json().catch(() => null);
      state.movementError = problem?.error ?? `API returned HTTP ${response.status}.`;
      updateMatchDataInPlace();
      return;
    }
    state.matchSnapshot = await response.json();
    updateMatchDataInPlace();
    if (activeMap?.getSource("territories")) {
      activeMap.getSource("territories").setData(territoryFeatureCollection());
    }
  } catch (error) {
    state.movementError = error instanceof Error ? error.message : "Could not select start position.";
    updateMatchDataInPlace();
  }
}

async function startGameFromMatch() {
  const gameId = currentGameId();
  const btn = document.querySelector('[data-action="start-game-from-match"]');
  if (btn) { btn.disabled = true; btn.textContent = "Starting…"; }
  state.movementError = null;
  try {
    const response = await fetch(`/api/games/${encodeURIComponent(gameId)}/start`, { method: "POST" });
    if (!response.ok) {
      const problem = await response.json().catch(() => null);
      state.movementError = problem?.error ?? `API returned HTTP ${response.status}.`;
      updateMatchDataInPlace();
      return;
    }
    state.matchSnapshot = await response.json();
    updateMatchDataInPlace();
    if (activeMap?.getSource("territories")) {
      activeMap.getSource("territories").setData(territoryFeatureCollection());
    }
  } catch (error) {
    state.movementError = error instanceof Error ? error.message : "Could not start game.";
    updateMatchDataInPlace();
  } finally {
    if (btn) { btn.disabled = false; btn.textContent = "Start Game"; }
  }
}

async function joinMatchSignalR(gameId) {
  if (!window.signalR) return;
  if (activeConnection?.state === "Connected" || activeConnection?.state === "Connecting") return;
  if (activeConnection) {
    try { await activeConnection.stop(); } catch {}
  }
  activeConnection = new window.signalR.HubConnectionBuilder()
    .withUrl("/hubs/match")
    .withAutomaticReconnect()
    .build();
  activeConnection.on("SnapshotUpdated", snapshot => {
    if (!isMatchRoute()) return;
    state.matchSnapshot = snapshot;
    if (activeMap?.getSource("territories")) {
      activeMap.getSource("territories").setData(territoryFeatureCollection());
    }
    updateMatchDataInPlace();
  });
  activeConnection.on("TerritoryActionResolved", event => {
    handleTerritoryActionResolved(event);
  });
  activeConnection.on("FactionEliminated", (data) => {
    if (!isMatchRoute()) return;
    const name = data?.eliminatedFactionName ?? "Unknown faction";
    const isPlayer = data?.eliminatorFactionId === state.myFactionId;
    showEliminationToast(name, isPlayer);
  });
  activeConnection.on("GameEnded", (data) => {
    if (!isMatchRoute()) return;
    state.gameEndedData = data ?? {};
    // Reload snapshot without clearing gameEndedData (don't null matchSnapshotGameId)
    state.matchSnapshot = null;
    state.matchLoading = false;
    void loadMatchSnapshot();
  });
  try {
    await activeConnection.start();
    await activeConnection.invoke("JoinMatchGroup", gameId);
  } catch {
    activeConnection = null;
  }
}

async function leaveMatchSignalR() {
  if (!activeConnection) return;
  const conn = activeConnection;
  activeConnection = null;
  try { await conn.stop(); } catch {}
}

function matchRoute(game) {
  if (game.id === "cardiff-match") return routes.match;
  return `/games/${encodeURIComponent(game.id)}`;
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

  if (target.dataset.action === "filter-games") {
    state.gamesFilter = target.dataset.filter ?? "all";
    render();
    return;
  }

  if (target.dataset.action === "save-player-name") {
    const input = document.getElementById("player-name");
    const btn = target.closest("[data-action='save-player-name']") ?? target;
    if (input) {
      const name = input.value.trim();
      state.playerName = name;
      localStorage.setItem("playerName", name);
      // Show brief confirmation on the button
      btn.textContent = "Saved ✓";
      btn.disabled = true;
      setTimeout(() => { btn.textContent = "Save"; btn.disabled = false; }, 1800);
      // If in a match, re-join with the new name
      if (isMatchRoute()) void savePlayerNameAndJoin();
    }
    return;
  }

  if (target.dataset.action === "start-game-from-match") {
    void startGameFromMatch();
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
    returnToGames();
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

  if (target.dataset.action === "toggle-game-option") {
    const key = target.dataset.optionKey;
    const current = state.gameOptions[key] !== false;
    state.gameOptions = setGameOption(localStorage, key, !current);
    updateMatchDataInPlace();
    return;
  }

  if (target.dataset.action === "dismiss-game-end") {
    state.gameEndOverlayDismissed = true;
    const overlay = document.querySelector("[data-game-end-overlay]");
    if (overlay) overlay.outerHTML = `<span data-game-end-overlay hidden></span>`;
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

window.__appState__ = state;

render();
