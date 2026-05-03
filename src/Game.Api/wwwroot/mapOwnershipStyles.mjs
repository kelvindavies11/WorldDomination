export const neutralTerritoryColor = "#dceee8";

export function ownerColorForTerritory(territory, factions) {
  const owner = factions.find(faction => faction.id === territory.ownerFactionId);
  return owner?.color ?? neutralTerritoryColor;
}

export function territoryFillPaint() {
  return {
    "fill-color": ["coalesce", ["get", "ownerColor"], neutralTerritoryColor],
    "fill-opacity": ["case", ["!=", ["get", "ownerFactionId"], null], 0.5, 0.22]
  };
}
