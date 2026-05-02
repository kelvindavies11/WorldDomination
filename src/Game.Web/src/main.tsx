import React, { useEffect, useState } from "react";
import { createRoot } from "react-dom/client";
import { createGame, fetchAvailableGames, type AvailableGame, type CreateGameRequest } from "./api/games";
import "./styles.css";

const routes = {
  games: "/games",
  create: "/games/create",
  match: "/games/cardiff"
};

function App() {
  const [path, setPath] = useState(window.location.pathname);
  const [games, setGames] = useState<AvailableGame[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const onPopState = () => setPath(window.location.pathname);
    window.addEventListener("popstate", onPopState);
    return () => window.removeEventListener("popstate", onPopState);
  }, []);

  useEffect(() => {
    if (path !== routes.games && path !== "/" && path !== "/games/cardiff/lobby") {
      return;
    }

    const controller = new AbortController();
    setIsLoading(true);
    fetchAvailableGames(controller.signal)
      .then(setGames)
      .catch(reason => {
        if (!controller.signal.aborted) {
          setError(reason instanceof Error ? reason.message : "Games could not be loaded.");
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) {
          setIsLoading(false);
        }
      });

    return () => controller.abort();
  }, [path]);

  function go(nextPath: string) {
    window.history.pushState({}, "", nextPath);
    setPath(nextPath);
  }

  async function onCreate(request: CreateGameRequest) {
    await createGame(request);
    setGames(await fetchAvailableGames());
    go(routes.games);
  }

  return (
    <div className="shell">
      <header className="topbar">
        <div className="topbar-inner">
          <button className="brand link-button" onClick={() => go(routes.games)}>Dynamic OSM World Domination</button>
          <nav className="nav" aria-label="Primary">
            <button onClick={() => go(routes.games)}>Available Games</button>
            <button onClick={() => go(routes.create)}>Create Game</button>
          </nav>
        </div>
      </header>
      <main>
        {path === routes.create
          ? <CreateGamePage go={go} onCreate={onCreate} />
          : path.startsWith("/games/") && path !== "/games/cardiff/lobby"
            ? <JoinedPage go={go} />
            : <GamesPage error={error} games={games} go={go} isLoading={isLoading} />}
      </main>
    </div>
  );
}

function GamesPage({ error, games, go, isLoading }: { error: string | null; games: AvailableGame[]; go: (path: string) => void; isLoading: boolean }) {
  return (
    <>
      <div className="page-head">
        <div>
          <h1>Available games</h1>
          <p className="subtitle">Join an open match or create a new one.</p>
        </div>
        <button onClick={() => go(routes.create)}>Create Game</button>
      </div>
      {isLoading && <div className="status">Loading available games...</div>}
      {error && <div className="status error">{error}</div>}
      {!isLoading && !error && <GameTable games={games} go={go} />}
    </>
  );
}

function GameTable({ games, go }: { games: AvailableGame[]; go: (path: string) => void }) {
  return (
    <section className="card">
      <div className="table">
        <div className="table-row table-head"><span>Name</span><span>Status</span><span>Players</span><span>Map</span><span>NPCs</span><span /></div>
        {games.map(game => (
          <div className="table-row" key={game.id}>
            <strong>{game.name}</strong>
            <span>{game.status}</span>
            <span>{game.humanPlayers}/{game.maxHumanPlayers}</span>
            <span>{game.mapArea}</span>
            <span>{game.npcFactions}</span>
            <button className="secondary" onClick={() => go(game.id === "cardiff-match" ? routes.match : `/games/${game.id}`)}>Join</button>
          </div>
        ))}
      </div>
    </section>
  );
}

function CreateGamePage({ go, onCreate }: { go: (path: string) => void; onCreate: (request: CreateGameRequest) => Promise<void> }) {
  const [error, setError] = useState<string | null>(null);

  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    try {
      await onCreate({
        name: String(form.get("name") ?? ""),
        mapArea: String(form.get("mapArea") ?? "Cardiff"),
        maxHumanPlayers: Number(form.get("maxHumanPlayers") ?? 2),
        npcFactions: Number(form.get("npcFactions") ?? 6),
        territoryCount: Number(form.get("territoryCount") ?? 100)
      });
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "The game could not be created.");
    }
  }

  return (
    <section className="card">
      <h1>Create game</h1>
      <p className="subtitle">Create an open game that appears in the available games list.</p>
      {error && <div className="status error">{error}</div>}
      <form className="form" onSubmit={submit}>
        <label>Game name<input name="name" defaultValue="Cardiff Skirmish" required /></label>
        <label>Map area<input name="mapArea" defaultValue="Cardiff" required /></label>
        <label>Max human players<input name="maxHumanPlayers" defaultValue="2" required /></label>
        <label>NPC factions<input name="npcFactions" defaultValue="6" required /></label>
        <label>Territories<input name="territoryCount" defaultValue="100" required /></label>
        <div className="actions">
          <button type="submit">Create Game</button>
          <button className="secondary" type="button" onClick={() => go(routes.games)}>Cancel</button>
        </div>
      </form>
    </section>
  );
}

function JoinedPage({ go }: { go: (path: string) => void }) {
  return <section className="card"><h1>Game joined</h1><p className="subtitle">The available-games flow is in place. The active match screen can be built next.</p><button className="secondary" onClick={() => go(routes.games)}>Back to Games</button></section>;
}

createRoot(document.getElementById("root")!).render(<App />);
