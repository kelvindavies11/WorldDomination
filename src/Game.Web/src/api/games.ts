export type AvailableGame = {
  id: string;
  name: string;
  status: string;
  mapArea: string;
  humanPlayers: number;
  maxHumanPlayers: number;
  npcFactions: number;
  territoryCount: number;
};

export type CreateGameRequest = {
  name: string;
  mapArea: string;
  maxHumanPlayers: number;
  npcFactions: number;
  territoryCount: number;
};

export async function fetchAvailableGames(signal?: AbortSignal): Promise<AvailableGame[]> {
  const response = await fetch("/api/games", { signal });

  if (!response.ok) {
    throw new Error(`Available games request failed with ${response.status}`);
  }

  return response.json() as Promise<AvailableGame[]>;
}

export async function createGame(request: CreateGameRequest): Promise<AvailableGame> {
  const response = await fetch("/api/games", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });

  if (!response.ok) {
    throw new Error(`Create game request failed with ${response.status}`);
  }

  return response.json() as Promise<AvailableGame>;
}
