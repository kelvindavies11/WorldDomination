import type { PrototypeMatchSnapshot } from "../../api/prototype";

export function createLobbyModel(snapshot: PrototypeMatchSnapshot) {
  const humanPlayers = snapshot.factions.filter(faction => faction.kind === "Human");
  const npcFactions = snapshot.factions.filter(faction => faction.kind === "Npc");
  const occupiedStarts = snapshot.territories.filter(territory => territory.ownerFactionId !== null).length;

  return {
    humanPlayers,
    npcFactions,
    summary: {
      mapArea: snapshot.mapArea,
      territories: snapshot.territories.length,
      armies: snapshot.armies.length,
      routes: snapshot.routes.length,
      occupiedStarts
    }
  };
}
