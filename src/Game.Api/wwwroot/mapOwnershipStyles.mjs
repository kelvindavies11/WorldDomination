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

export function attackTrailPaint() {
  return {
    "line-color": ["get", "attackColor"],
    "line-width": [
      "interpolate",
      ["linear"],
      ["get", "progress"],
      0,
      2,
      0.7,
      5,
      1,
      1.4
    ],
    "line-opacity": [
      "interpolate",
      ["linear"],
      ["get", "progress"],
      0,
      0.18,
      0.25,
      0.92,
      1,
      0
    ],
    "line-blur": 0.5
  };
}

export function attackImpactPaint() {
  return {
    "fill-color": ["get", "impactColor"],
    "fill-opacity": [
      "interpolate",
      ["linear"],
      ["get", "progress"],
      0,
      0,
      0.18,
      0.38,
      1,
      0
    ],
    "fill-outline-color": ["get", "impactColor"]
  };
}
