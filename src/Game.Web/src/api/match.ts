export type FactionKind = "Human" | "Npc";

export type TerritoryStats = {
  economy: number;
  defense: number;
  mobility: number;
  strategicValue: number;
};

export type MatchFaction = {
  id: string;
  name: string;
  kind: FactionKind;
  color: string;
};

export type MatchTerritory = {
  id: string;
  index: number;
  name: string;
  areaSquareKm: number;
  ownerFactionId: string | null;
  stats: TerritoryStats;
  postcode: string;
  features: Record<string, number>;
  boundaryCoordinates: MapCoordinate[];
};

export type MapCoordinate = {
  longitude: number;
  latitude: number;
};

export type MatchMap = {
  id: string;
  name: string;
  center: MapCoordinate;
  cameraBounds: MapCoordinate[];
  boundaryCoordinates: MapCoordinate[];
};

export type MatchArmy = {
  id: string;
  factionId: string;
  territoryId: string;
  strength: number;
};

export type MatchRoute = {
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

export type MatchSnapshot = {
  gameId: string;
  mapArea: string;
  snapshotGeneratedAtUtc: string;
  map: MatchMap;
  factions: MatchFaction[];
  territories: MatchTerritory[];
  armies: MatchArmy[];
  routes: MatchRoute[];
  leaderboard: LeaderboardRow[];
};

export async function fetchCardiffMatch(signal?: AbortSignal): Promise<MatchSnapshot> {
  const response = await fetch("/api/matches/cardiff", { signal });

  if (!response.ok) {
    throw new Error(`Cardiff match request failed with ${response.status}`);
  }

  return response.json() as Promise<MatchSnapshot>;
}
