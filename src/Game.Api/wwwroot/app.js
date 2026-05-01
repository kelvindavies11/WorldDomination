const app = document.querySelector("#app");

const state = {
  games: [],
  loading: false,
  error: null,
  creating: false,
  createError: null
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
    <div class="page-head">
      <div>
        <h1>Game joined</h1>
        <p class="subtitle">The available-games flow is now in place. The active match screen can be built next.</p>
      </div>
      <a class="button secondary" href="${routes.games}" data-link>Back to Games</a>
    </div>
    <section class="card">
      <h2>Ready for match setup</h2>
      <p class="muted">This page is a destination for Join while the actual game screen remains a later slice.</p>
    </section>
  `);
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
  app.innerHTML = route();
}

function matchRoute(game) {
  return game.id === "cardiff-prototype"
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
