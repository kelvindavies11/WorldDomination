export function applyTerritorySelection(state, id, validTargetTerritoryIds = []) {
  const territory = state.matchSnapshot?.territories?.find(item => item.id === id);
  if (!territory) {
    return false;
  }

  state.selectedTerritoryId = id;
  state.movementError = null;

  if (state.selectedSourceTerritoryId && validTargetTerritoryIds.includes(id)) {
    state.selectedTargetTerritoryId = id;
    state.selectedMovementStrength = Math.min(
      Math.max(state.selectedMovementStrength, 1),
      armyStrengthForTerritory(state, state.selectedSourceTerritoryId, "human-1"));
    return true;
  }

  if (territory.ownerFactionId === "human-1") {
    state.selectedSourceTerritoryId = id;
    state.selectedTargetTerritoryId = null;
    state.selectedMovementStrength = Math.max(1, Math.floor(armyStrengthForTerritory(state, id, "human-1") / 2));
    return true;
  }

  state.selectedSourceTerritoryId = null;
  state.selectedTargetTerritoryId = null;
  state.selectedMovementStrength = 1;
  return true;
}

function armyStrengthForTerritory(state, territoryId, factionId) {
  if (!territoryId || !factionId || !state.matchSnapshot?.armies) {
    return 0;
  }

  return state.matchSnapshot.armies
    .filter(army => army.territoryId === territoryId && army.factionId === factionId)
    .reduce((total, army) => total + army.strength, 0);
}
