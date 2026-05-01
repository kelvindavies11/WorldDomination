export type FactionKind = "Human" | "Npc";

export type TerritoryStats = {
  economy: number;
  defense: number;
  mobility: number;
  strategicValue: number;
};

export type PrototypeFaction = {
  id: string;
  name: string;
  kind: FactionKind;
  color: string;
};

export type PrototypeTerritory = {
  id: string;
  index: number;
  name: string;
  areaSquareKm: number;
  ownerFactionId: string | null;
  stats: TerritoryStats;
};

export type PrototypeArmy = {
  id: string;
  factionId: string;
  territoryId: string;
  strength: number;
};

export type PrototypeRoute = {
  sourceTerritoryId: string;
  destinationTerritoryId: string;
  transport: string;
  etaSeconds: number;
  isAllowed: boolean;
};

export type LeaderboardRow = {
  rank: number;
  factionId: string;
  factionName: string;
  mapControlPercentage: number;
  eliminationCount: number;
  isEliminated: boolean;
};

export type PrototypeMatchSnapshot = {
  gameId: string;
  mapArea: string;
  factions: PrototypeFaction[];
  territories: PrototypeTerritory[];
  armies: PrototypeArmy[];
  routes: PrototypeRoute[];
  leaderboard: LeaderboardRow[];
};

export async function fetchCardiffPrototype(signal?: AbortSignal): Promise<PrototypeMatchSnapshot> {
  const response = await fetch("/api/prototype/cardiff", { signal });

  if (!response.ok) {
    throw new Error(`Cardiff prototype request failed with ${response.status}`);
  }

  return response.json() as Promise<PrototypeMatchSnapshot>;
}
