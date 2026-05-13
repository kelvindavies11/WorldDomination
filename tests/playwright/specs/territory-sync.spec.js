// @ts-check
/**
 * Two-browser territory-sync test.
 *
 * Verifies that when Player A claims a start territory, Player B's open match
 * page receives the SignalR push and reflects the updated snapshot without any
 * page reload.
 *
 * Requires the API server to be running: dotnet run --project src/Game.Api/Game.Api.csproj
 */

const { test, expect } = require("@playwright/test");

const BASE = "http://localhost:5057";

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/** Create a unique deterministic player identity for this test run. */
function makePlayer(tag) {
  const id = `pw-test-${tag}-${Date.now()}`;
  return { id, name: `PW ${tag}` };
}

/**
 * Inject localStorage player identity into a page before navigation so the
 * app picks it up on first render instead of generating one.
 */
async function setPlayerIdentity(page, player) {
  await page.addInitScript(({ id, name }) => {
    localStorage.setItem("dynamic-osm-player-id", id);
    localStorage.setItem("dynamic-osm-player-name", name);
  }, player);
}

/**
 * POST a JSON request using the page's fetch so it shares the same origin
 * (avoids CORS) and carries the right player headers.
 */
async function apiFetch(page, player, method, path, body) {
  return page.evaluate(
    async ({ base, method, path, body, playerId, playerName }) => {
      const response = await fetch(`${base}${path}`, {
        method,
        headers: {
          "Content-Type": "application/json",
          "X-Player-Id": playerId,
          "X-Player-Name": playerName,
        },
        body: body ? JSON.stringify(body) : undefined,
      });
      const text = await response.text();
      return { status: response.status, body: text ? JSON.parse(text) : null };
    },
    { base: BASE, method, path, body, playerId: player.id, playerName: player.name }
  );
}

// ---------------------------------------------------------------------------
// Test
// ---------------------------------------------------------------------------

test("Player B sees Player A's territory claim via SignalR without reload", async ({ browser }) => {
  const playerA = makePlayer("A");
  const playerB = makePlayer("B");

  // Open two independent browser contexts so they each have isolated cookies /
  // localStorage — simulating two real users on different tabs.
  const ctxA = await browser.newContext();
  const ctxB = await browser.newContext();

  try {
    const pageA = await ctxA.newPage();
    const pageB = await ctxB.newPage();

    // Set identities before the pages load.
    await setPlayerIdentity(pageA, playerA);
    await setPlayerIdentity(pageB, playerB);

    // -----------------------------------------------------------------------
    // 1. Create a game via REST (from Player A's context so the right headers
    //    are sent — player A becomes the creator / first player).
    // -----------------------------------------------------------------------
    await pageA.goto(`${BASE}/games`);

    const createResult = await apiFetch(pageA, playerA, "POST", "/api/games", {
      name: `PW Sync Test ${Date.now()}`,
      mapArea: "Cardiff",
      maxHumanPlayers: 2,
      npcFactions: 1,
      territoryCount: 100,
    });
    expect(createResult.status, "Game creation should succeed").toBe(201);
    const gameId = createResult.body.id;
    expect(gameId).toBeTruthy();
    const matchUrl = `${BASE}/games/${encodeURIComponent(gameId)}`;

    // -----------------------------------------------------------------------
    // 2. Both players open the match page — the app auto-joins on load.
    //    Player A navigated first; now navigate Player B to the same URL.
    // -----------------------------------------------------------------------
    await pageA.goto(matchUrl);
    // The name-prompt intercept only fires if playerName is unset; we set it
    // above, so the match page should render directly.
    await pageA.waitForFunction(
      () => window.__appState__?.matchSnapshot != null,
      { timeout: 15_000 }
    );

    await pageB.goto(matchUrl);
    await pageB.waitForFunction(
      () => window.__appState__?.matchSnapshot != null,
      { timeout: 15_000 }
    );

    // -----------------------------------------------------------------------
    // 3. Capture initial state in Player B's page.
    // -----------------------------------------------------------------------
    const initialSnapshotGeneratedAt = await pageB.evaluate(
      () => window.__appState__.matchSnapshot?.snapshotGeneratedAtUtc ?? null
    );
    expect(initialSnapshotGeneratedAt).toBeTruthy();

    // -----------------------------------------------------------------------
    // 4. Pick a neutral territory for Player A to claim as their start.
    // -----------------------------------------------------------------------
    const neutralTerritoryId = await pageA.evaluate(() => {
      const territories = window.__appState__.matchSnapshot?.territories ?? [];
      return territories.find((t) => t.ownerFactionId == null)?.id ?? null;
    });
    expect(neutralTerritoryId, "There should be at least one neutral territory").toBeTruthy();

    // -----------------------------------------------------------------------
    // 5. Player A claims the territory via the API.  Using apiFetch so we
    //    don't need to fight the WebGL map canvas to pick a territory.
    // -----------------------------------------------------------------------
    const claimResult = await apiFetch(pageA, playerA, "POST", `/api/games/${encodeURIComponent(gameId)}/start-position`, {
      territoryId: neutralTerritoryId,
    });
    expect(claimResult.status, "Start position claim should succeed").toBe(200);

    // -----------------------------------------------------------------------
    // 6. Player B should receive a SnapshotUpdated push via SignalR.
    //    We wait for the snapshot timestamp to change (the server rebuilds the
    //    snapshot and pushes it), or for the territory to show an owner.
    // -----------------------------------------------------------------------
    await pageB.waitForFunction(
      ({ initialTs, territoryId }) => {
        const snapshot = window.__appState__?.matchSnapshot;
        if (!snapshot) return false;
        // Accept if the snapshot was regenerated after the claim
        if (snapshot.snapshotGeneratedAtUtc !== initialTs) return true;
        // Or if the territory now has an owner
        const t = snapshot.territories?.find((x) => x.id === territoryId);
        return t?.ownerFactionId != null;
      },
      { initialTs: initialSnapshotGeneratedAt, territoryId: neutralTerritoryId },
      { timeout: 10_000 }
    );

    const claimedTerritory = await pageB.evaluate(({ territoryId }) => {
      return window.__appState__.matchSnapshot?.territories?.find((t) => t.id === territoryId) ?? null;
    }, { territoryId: neutralTerritoryId });

    expect(claimedTerritory?.ownerFactionId, "Player B should see the claimed territory has an owner").toBeTruthy();

    // -----------------------------------------------------------------------
    // 7. (Bonus) Start the game: Player B also claims a start, then Player A
    //    starts the game.  Player B sees the game transition to Started.
    // -----------------------------------------------------------------------
    const neutralForB = await pageB.evaluate(() => {
      const territories = window.__appState__.matchSnapshot?.territories ?? [];
      return territories.find((t) => t.ownerFactionId == null)?.id ?? null;
    });

    if (neutralForB) {
      await apiFetch(pageB, playerB, "POST", `/api/games/${encodeURIComponent(gameId)}/start-position`, {
        territoryId: neutralForB,
      });

      const startResult = await apiFetch(pageA, playerA, "POST", `/api/games/${encodeURIComponent(gameId)}/start`, null);
      expect(startResult.status, "Game start should succeed").toBe(200);

      // Both pages should receive SnapshotUpdated reflecting "Started" game status.
      for (const [label, page] of [["A", pageA], ["B", pageB]]) {
        await page.waitForFunction(
          () => window.__appState__?.matchSnapshot?.game?.isStarted === true,
          { timeout: 10_000 }
        );
        const status = await page.evaluate(() => window.__appState__.matchSnapshot?.game?.status);
        expect(status, `Player ${label} should see Started status`).toBe("Started");
      }

      // -----------------------------------------------------------------------
      // 8. Player A sends a movement to an adjacent neutral territory.
      //    Player B's snapshot should update without any manual refresh.
      // -----------------------------------------------------------------------
      const playerAFactionId = await pageA.evaluate(() => window.__appState__.playerFactionId);
      const moveSetup = await pageA.evaluate(({ factionId }) => {
        const snapshot = window.__appState__.matchSnapshot;
        // Find a territory owned by Player A
        const source = snapshot.territories.find((t) => t.ownerFactionId === factionId);
        if (!source) return null;
        // Find a neutral territory connected via a route from the source
        const adjacent = snapshot.routes
          ?.filter((r) => r.isAllowed && (r.sourceTerritoryId === source.id || r.destinationTerritoryId === source.id))
          ?.map((r) => r.sourceTerritoryId === source.id ? r.destinationTerritoryId : r.sourceTerritoryId)
          ?? [];
        const target = snapshot.territories.find((t) => adjacent.includes(t.id) && t.ownerFactionId == null);
        if (!target) return null;
        return { sourceId: source.id, targetId: target.id };
      }, { factionId: playerAFactionId });

      if (moveSetup) {
        const beforeTs = await pageB.evaluate(() => window.__appState__.matchSnapshot?.snapshotGeneratedAtUtc);

        await apiFetch(pageA, playerA, "POST", `/api/matches/${encodeURIComponent(gameId)}/movements`, {
          playerFactionId: playerAFactionId,
          sourceTerritoryId: moveSetup.sourceId,
          targetTerritoryId: moveSetup.targetId,
          strength: 1,
        });

        // Player B's snapshot should refresh via SignalR push
        await pageB.waitForFunction(
          ({ ts }) => window.__appState__?.matchSnapshot?.snapshotGeneratedAtUtc !== ts,
          { ts: beforeTs },
          { timeout: 10_000 }
        );

        const movedTerritory = await pageB.evaluate(({ targetId }) => {
          return window.__appState__.matchSnapshot?.territories?.find((t) => t.id === targetId) ?? null;
        }, { targetId: moveSetup.targetId });

        expect(movedTerritory?.ownerFactionId, "Player B should see the captured territory has an owner after movement").toBeTruthy();
      }
    }
  } finally {
    await ctxA.close();
    await ctxB.close();
  }
});
