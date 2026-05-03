import { ownerColorForTerritory, territoryFillPaint } from "./mapOwnershipStyles.mjs";

const app = document.querySelector("#app");

const state = {
  games: [],
  loading: false,
  error: null,
  creating: false,
  createError: null,
  matchSnapshot: null,
  matchLoading: false,
  matchError: null,
  selectedTerritoryId: null,
  collapsedWidgets: new Set()
};

let activeMap = null;
let mapInitializedForPath = null;
let hoveredTerritoryId = null;

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
  if (!state.loading && state.games.length === 0 && !state.error) {
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
    return `<section class="card"><h2>No games available</h2><p class="muted">Create a game to open the lobby.</p></section>`;
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
      <form class="form" data-action="create-game">
        <div class="field">
          <label for="name">Game name</label>
          <input id="name" name="name" value="Cardiff Skirmish" required>
        </div>
        <div class="field">
          <label for="mapArea">Map area</label>
          <select id="mapArea" name="mapArea">
            <option>Cardiff</option>
          </select>
        </div>
        <div class="field">
          <label for="maxHumanPlayers">Max human players</label>
          <input id="maxHumanPlayers" name="maxHumanPlayers" value="2" inputmode="numeric" required>
        </div>
        <div class="field">
          <label for="npcFactions">NPC factions</label>
          <input id="npcFactions" name="npcFactions" value="6" inputmode="numeric" required>
        </div>
        <div class="field">
          <label for="territoryCount">Territories</label>
          <input id="territoryCount" name="territoryCount" value="100" inputmode="numeric" required>
        </div>
        <div class="actions">
          <button type="submit">${state.creating ? "Creating..." : "Create Game"}</button>
          <a class="button secondary" href="${routes.games}" data-link>Cancel</a>
        </div>
      </form>
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
        </div>

        <aside class="floating-widget selected-territory-widget ${widgetCollapsedClass("selected-territory")}" aria-label="Selected territory">
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

            <div class="stat-list" data-selected-stats>
              ${territoryStatsMarkup(selectedTerritory())}
            </div>

            <div class="panel-section">
              <h3>Army</h3>
              <div class="army-card">
                <span class="army-strength">100</span>
                <span class="muted">starting strength</span>
              </div>
            </div>
          </div>
        </aside>

        <aside class="floating-widget leaderboard-widget ${widgetCollapsedClass("leaderboard")}" aria-label="Leaderboard and match status">
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

function territoryStatsMarkup(territory) {
  const stats = territory?.stats ?? {
    economy: 74,
    defense: 58,
    mobility: 82,
    strategicValue: 77
  };

  return `
    ${statItem("Economy", stats.economy)}
    ${statItem("Defense", stats.defense)}
    ${statItem("Mobility", stats.mobility)}
    ${statItem("Value", stats.strategicValue)}
  `;
}

function statItem(label, value) {
  return `
    <div class="stat-item">
      <span>${label}</span>
      <strong>${Math.round(value)}</strong>
    </div>
  `;
}

function leaderboardMarkup() {
  const rows = state.matchSnapshot?.leaderboard;
  const fallbackRows = [
    { factionName: "You", mapControlPercentage: 18, eliminationCount: 0, color: "#1f8a70" },
    { factionName: "Player 2", mapControlPercentage: 14, eliminationCount: 0, color: "#2f6fbd" },
    { factionName: "NPC-1", mapControlPercentage: 9, eliminationCount: 0, color: "#c58a1a" }
  ];

  return (rows?.length ? rows : fallbackRows).slice(0, 6).map(row => {
    const control = Math.round(row.mapControlPercentage);
    const color = row.color ?? factionById(row.factionId)?.color ?? "#1f8a70";
    return `
      <div class="leader-entry">
        <span class="leader-swatch" style="--swatch:${escapeHtml(color)}"></span>
        <strong>${escapeHtml(row.factionName)}</strong>
        <span>${control}%</span>
        <small>${row.eliminationCount} elim</small>
        <div class="bar"><div class="fill" style="--w:${control}%; --fill:${escapeHtml(color)}"></div></div>
      </div>
    `;
  }).join("");
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
  } catch (error) {
    state.error = error instanceof Error ? error.message : "Games could not be loaded.";
  } finally {
    state.loading = false;
    render();
  }
}

async function loadMatchSnapshot() {
  if (state.matchLoading || state.matchSnapshot || state.matchError) {
    return;
  }

  state.matchLoading = true;
  updateMatchSummary();

  try {
    const response = await fetch("/api/matches/cardiff");
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
    territoryCount: numberValue(formData, "territoryCount")
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
    window.history.pushState({}, "", routes.games);
    await loadGames();
  } catch (error) {
    state.createError = error instanceof Error ? error.message : "The game could not be created.";
    render();
  } finally {
    state.creating = false;
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

  if (!map.getLayer("territory-selected-outline")) {
    map.addLayer({
      id: "territory-selected-outline",
      type: "line",
      source: "territories",
      filter: ["==", ["get", "id"], state.selectedTerritoryId ?? ""],
      paint: {
        "line-color": "#ffffff",
        "line-width": 2.4,
        "line-opacity": 0.82
      }
    });
  }

  map.on("click", "territory-fill", event => {
    const feature = event.features?.[0];
    const id = feature?.properties?.id;
    if (!id) {
      return;
    }

    state.selectedTerritoryId = id;
    map.setFilter("territory-selected-outline", ["==", ["get", "id"], id]);
    updateMatchDataInPlace();
  });

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

  const selectedStats = document.querySelector("[data-selected-stats]");
  if (selectedStats) {
    selectedStats.innerHTML = territoryStatsMarkup(selected);
  }

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
