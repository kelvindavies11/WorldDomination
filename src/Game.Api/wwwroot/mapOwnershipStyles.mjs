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

export function captureExpansionFillPaint() {
  return {
    "fill-color": ["get", "ownerColor"],
    "fill-opacity": [
      "interpolate",
      ["linear"],
      ["get", "progress"],
      0,
      0.12,
      0.72,
      0.48,
      1,
      0
    ],
    "fill-outline-color": ["get", "ownerColor"]
  };
}
