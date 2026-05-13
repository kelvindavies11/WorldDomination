export function validTargetTerritoryIds(snapshot, sourceId, playerFactionId = "human-1") {
  if (!sourceId || !snapshot) {
    return [];
  }

  const territoriesById = new Map((snapshot.territories ?? []).map(territory => [territory.id, territory]));
  const seen = new Set();
  return (snapshot.routes ?? [])
    .filter(route => route.isAllowed && (route.sourceTerritoryId === sourceId || route.destinationTerritoryId === sourceId))
    .map(route => route.sourceTerritoryId === sourceId ? route.destinationTerritoryId : route.sourceTerritoryId)
    .filter(id => {
      const owner = territoriesById.get(id)?.ownerFactionId;
      const isFriendly = owner === playerFactionId;
      if (isFriendly || seen.has(id)) {
        return false;
      }

      seen.add(id);
      return true;
    });
}

export function reinforceTargetIds(snapshot, sourceId, playerFactionId = "human-1") {
  if (!sourceId || !snapshot) {
    return [];
  }

  const territoriesById = new Map((snapshot.territories ?? []).map(territory => [territory.id, territory]));
  const seen = new Set();
  return (snapshot.routes ?? [])
    .filter(route => route.isAllowed && (route.sourceTerritoryId === sourceId || route.destinationTerritoryId === sourceId))
    .map(route => route.sourceTerritoryId === sourceId ? route.destinationTerritoryId : route.sourceTerritoryId)
    .filter(id => {
      if (id === sourceId || seen.has(id)) {
        return false;
      }

      const owner = territoriesById.get(id)?.ownerFactionId;
      const isFriendly = owner === playerFactionId;
      if (!isFriendly) {
        return false;
      }

      seen.add(id);
      return true;
    });
}

export function routeBetween(snapshot, sourceId, targetId) {
  return (snapshot?.routes ?? []).find(route =>
    route.sourceTerritoryId === sourceId && route.destinationTerritoryId === targetId ||
    route.sourceTerritoryId === targetId && route.destinationTerritoryId === sourceId) ?? null;
}
