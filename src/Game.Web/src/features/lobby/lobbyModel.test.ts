import { describe, expect, it } from "vitest";
import type { MatchSnapshot } from "../../api/match";
import { createLobbyModel } from "./lobbyModel";

const snapshot: MatchSnapshot = {
  gameId: "cardiff-match",
  mapArea: "Cardiff",
  factions: [
    { id: "human-1", name: "Player 1", kind: "Human", color: "#1f8a70" },
    { id: "human-2", name: "Player 2", kind: "Human", color: "#2f6fbd" },
    { id: "npc-1", name: "NPC 1", kind: "Npc", color: "#c58a1a" }
  ],
  territories: [
    {
      id: "territory-000",
      index: 0,
      name: "Cardiff Sector 1",
      areaSquareKm: 1,
      ownerFactionId: "human-1",
      stats: { economy: 10, defense: 20, mobility: 30, strategicValue: 40 }
    },
    {
      id: "territory-001",
      index: 1,
      name: "Cardiff Sector 2",
      areaSquareKm: 1.2,
      ownerFactionId: null,
      stats: { economy: 11, defense: 21, mobility: 31, strategicValue: 41 }
    }
  ],
  armies: [
    { id: "army-human-1", factionId: "human-1", territoryId: "territory-000", strength: 100 }
  ],
  routes: [
    { sourceTerritoryId: "territory-000", destinationTerritoryId: "territory-001", transport: "Road", etaSeconds: 90, isAllowed: true }
  ],
  leaderboard: []
};

describe("createLobbyModel", () => {
  it("separates human players and NPC factions", () => {
    const model = createLobbyModel(snapshot);

    expect(model.humanPlayers.map(player => player.name)).toEqual(["Player 1", "Player 2"]);
    expect(model.npcFactions.map(faction => faction.name)).toEqual(["NPC 1"]);
  });

  it("summarizes territory and route counts", () => {
    const model = createLobbyModel(snapshot);

    expect(model.summary).toEqual({
      mapArea: "Cardiff",
      territories: 2,
      armies: 1,
      routes: 1,
      occupiedStarts: 1
    });
  });
});
