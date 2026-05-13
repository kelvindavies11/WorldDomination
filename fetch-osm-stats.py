#!/usr/bin/env python3
"""Fetch real OSM feature counts for 16 Newport postcode sectors via Overpass API."""

import json, math, time, urllib.request, urllib.parse, sys
from pathlib import Path

SECTORS = [
    {"postcode": "NP10 8", "S": 51.490, "W": -3.090, "N": 51.540, "E": -3.010},
    {"postcode": "NP18 2", "S": 51.490, "W": -3.010, "N": 51.540, "E": -2.900},
    {"postcode": "NP10 4", "S": 51.540, "W": -3.090, "N": 51.580, "E": -3.040},
    {"postcode": "NP20 4", "S": 51.540, "W": -3.040, "N": 51.580, "E": -2.990},
    {"postcode": "NP19 8", "S": 51.540, "W": -2.990, "N": 51.580, "E": -2.940},
    {"postcode": "NP19 4", "S": 51.540, "W": -2.940, "N": 51.580, "E": -2.900},
    {"postcode": "NP10 3", "S": 51.580, "W": -3.090, "N": 51.620, "E": -3.050},
    {"postcode": "NP20 1", "S": 51.580, "W": -3.050, "N": 51.620, "E": -3.010},
    {"postcode": "NP20 0", "S": 51.580, "W": -3.010, "N": 51.620, "E": -2.970},
    {"postcode": "NP19 0", "S": 51.580, "W": -2.970, "N": 51.620, "E": -2.930},
    {"postcode": "NP19 9", "S": 51.580, "W": -2.930, "N": 51.620, "E": -2.900},
    {"postcode": "NP10 0", "S": 51.620, "W": -3.090, "N": 51.655, "E": -3.050},
    {"postcode": "NP20 2", "S": 51.620, "W": -3.050, "N": 51.655, "E": -3.010},
    {"postcode": "NP20 3", "S": 51.620, "W": -3.010, "N": 51.655, "E": -2.970},
    {"postcode": "NP19 7", "S": 51.620, "W": -2.970, "N": 51.655, "E": -2.930},
    {"postcode": "NP18 1", "S": 51.620, "W": -2.930, "N": 51.655, "E": -2.900},
]

OVERPASS_URL = "https://overpass-api.de/api/interpreter"


def query_overpass(q: str, timeout: int = 55) -> list:
    """POST an Overpass QL query and return the elements list."""
    data = urllib.parse.urlencode({"data": q}).encode("utf-8")
    req = urllib.request.Request(
        OVERPASS_URL, data=data,
        headers={"Content-Type": "application/x-www-form-urlencoded",
                 "User-Agent": "game-osm-stats/1.0"}
    )
    with urllib.request.urlopen(req, timeout=timeout) as resp:
        body = resp.read().decode("utf-8")
    return json.loads(body).get("elements", [])


def count_features(elements: list) -> dict:
    """Count OSM features using the same logic as OsmTerritoryFeatureMapper.cs."""
    f = dict(Shops=0, Factories=0, CommercialAreas=0, Offices=0, IndustrialSites=0,
             FarmlandOrResources=0, PopulationSupport=0, Mountains=0, Hills=0,
             MilitarySites=0, CastlesOrForts=0, GovernmentSites=0, Chokepoints=0,
             UrbanDensity=0, Roads=0, Railways=0, BridgesOrTunnels=0,
             Airports=0, Ports=0, SpecialFeatures=0)
    for el in elements:
        t = el.get("tags", {})
        if "shop" in t or t.get("amenity") == "marketplace":
            f["Shops"] += 1
        if t.get("building") == "industrial" or t.get("man_made") in ("works", "factory"):
            f["Factories"] += 1
        if t.get("landuse") in ("commercial", "retail"):
            f["CommercialAreas"] += 1
        if "office" in t:
            f["Offices"] += 1
        if t.get("landuse") == "industrial" or t.get("building") in ("warehouse", "industrial"):
            f["IndustrialSites"] += 1
        if (t.get("landuse") in ("farmland", "farmyard", "quarry")
                or t.get("man_made") in ("petroleum_well", "power_plant", "water_works")
                or "power" in t):
            f["FarmlandOrResources"] += 1
        if (t.get("landuse") == "residential"
                or t.get("amenity") in ("school", "college", "university", "hospital", "clinic", "doctors")):
            f["PopulationSupport"] += 1
        if t.get("natural") in ("peak", "mountain_range"):
            f["Mountains"] += 1
        if t.get("natural") in ("hill", "ridge") or "ele" in t:
            f["Hills"] += 1
        if "military" in t or t.get("landuse") == "military":
            f["MilitarySites"] += 1
            f["SpecialFeatures"] += 1
        if t.get("historic") in ("castle", "fort", "citywalls") or t.get("building") == "castle":
            f["CastlesOrForts"] += 1
            f["SpecialFeatures"] += 1
        if (t.get("office") == "government"
                or t.get("amenity") in ("townhall", "courthouse", "police", "fire_station")):
            f["GovernmentSites"] += 1
        if ("waterway" in t
                or t.get("natural") in ("water", "coastline")
                or t.get("barrier") in ("retaining_wall", "city_wall")):
            f["Chokepoints"] += 1
        if "building" in t or t.get("landuse") in ("residential", "commercial", "retail"):
            f["UrbanDensity"] += 1
        if "highway" in t:
            f["Roads"] += 1
        if "railway" in t:
            f["Railways"] += 1
        if "bridge" in t or "tunnel" in t:
            f["BridgesOrTunnels"] += 1
        if "aeroway" in t:
            f["Airports"] += 1
            f["SpecialFeatures"] += 1
        if (t.get("amenity") == "ferry_terminal"
                or t.get("harbour") == "yes"
                or t.get("leisure") == "marina"):
            f["Ports"] += 1
            f["SpecialFeatures"] += 1
        if t.get("amenity") in ("hospital", "university") or t.get("power") in ("plant", "substation"):
            f["SpecialFeatures"] += 1
    return f


def bbox_area_km2(S, N, W, E) -> float:
    lat_km = (N - S) * 111.0
    lng_km = (E - W) * 111.0 * math.cos(math.radians((S + N) / 2))
    return round(lat_km * lng_km, 2)


def main():
    features_path = Path(__file__).parent / "src/Game.Application/Data/cardiff-territory-features.json"
    existing = json.loads(features_path.read_text(encoding="utf-8"))

    results = {}
    for s in SECTORS:
        code = s["postcode"]
        S, W, N, E = s["S"], s["W"], s["N"], s["E"]
        q = f"[out:json][timeout:45];\n(\n  node({S},{W},{N},{E});\n  way({S},{W},{N},{E});\n);\nout tags;"
        print(f"Querying {code} ...", end=" ", flush=True)
        try:
            elements = query_overpass(q)
            counts = count_features(elements)
            counts["AreaSquareKm"] = bbox_area_km2(S, N, W, E)
            counts["Connections"] = 5
            results[code] = counts
            print(f"{len(elements)} elements, area={counts['AreaSquareKm']} km2, "
                  f"shops={counts['Shops']} roads={counts['Roads']} urban={counts['UrbanDensity']}")
        except Exception as ex:
            print(f"ERROR: {ex}", file=sys.stderr)
        time.sleep(1.5)  # respect Overpass rate limit

    print(f"\nFetched {len(results)}/{len(SECTORS)} sectors")
    if not results:
        print("Nothing to update.")
        return

    # Merge into existing JSON (replace NP entries only)
    for code, counts in results.items():
        existing[code] = {
            "Airports": counts["Airports"],
            "AreaSquareKm": counts["AreaSquareKm"],
            "BridgesOrTunnels": counts["BridgesOrTunnels"],
            "CastlesOrForts": counts["CastlesOrForts"],
            "Chokepoints": counts["Chokepoints"],
            "CommercialAreas": counts["CommercialAreas"],
            "Connections": counts["Connections"],
            "Factories": counts["Factories"],
            "FarmlandOrResources": counts["FarmlandOrResources"],
            "GovernmentSites": counts["GovernmentSites"],
            "Hills": counts["Hills"],
            "IndustrialSites": counts["IndustrialSites"],
            "MilitarySites": counts["MilitarySites"],
            "Mountains": counts["Mountains"],
            "Offices": counts["Offices"],
            "PopulationSupport": counts["PopulationSupport"],
            "Ports": counts["Ports"],
            "Railways": counts["Railways"],
            "Roads": counts["Roads"],
            "Shops": counts["Shops"],
            "SpecialFeatures": counts["SpecialFeatures"],
            "UrbanDensity": counts["UrbanDensity"],
        }

    features_path.write_text(json.dumps(existing, indent=2), encoding="utf-8")
    print(f"Updated {features_path} with real OSM data for {len(results)} sectors.")


if __name__ == "__main__":
    main()
