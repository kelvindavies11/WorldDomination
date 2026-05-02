using Game.Domain;

namespace Game.Application;

public static class OsmTerritoryFeatureMapper
{
    public static TerritoryFeatureSummary Map(
        IEnumerable<IReadOnlyDictionary<string, string>> tagSets,
        double areaSquareKm,
        int connectionCount)
    {
        var factories = 0;
        var shops = 0;
        var commercialAreas = 0;
        var offices = 0;
        var industrialSites = 0;
        var farmlandOrResources = 0;
        var populationSupport = 0;
        var mountains = 0;
        var hills = 0;
        var militarySites = 0;
        var castlesOrForts = 0;
        var governmentSites = 0;
        var chokepoints = 0;
        var urbanDensity = 0;
        var roads = 0;
        var railways = 0;
        var bridgesOrTunnels = 0;
        var airports = 0;
        var ports = 0;
        var specialFeatures = 0;

        foreach (var tags in tagSets)
        {
            if (HasKey(tags, "shop") || HasValue(tags, "amenity", "marketplace"))
            {
                shops++;
            }

            if (HasValue(tags, "building", "industrial") ||
                HasAnyValue(tags, "man_made", "works", "factory"))
            {
                factories++;
            }

            if (HasAnyValue(tags, "landuse", "commercial", "retail"))
            {
                commercialAreas++;
            }

            if (HasKey(tags, "office"))
            {
                offices++;
            }

            if (HasAnyValue(tags, "landuse", "industrial") ||
                HasAnyValue(tags, "building", "warehouse", "industrial"))
            {
                industrialSites++;
            }

            if (HasAnyValue(tags, "landuse", "farmland", "farmyard", "quarry") ||
                HasAnyValue(tags, "man_made", "petroleum_well", "power_plant", "water_works") ||
                HasKey(tags, "power"))
            {
                farmlandOrResources++;
            }

            if (HasAnyValue(tags, "landuse", "residential") ||
                HasAnyValue(tags, "amenity", "school", "college", "university", "hospital", "clinic", "doctors"))
            {
                populationSupport++;
            }

            if (HasAnyValue(tags, "natural", "peak", "mountain_range"))
            {
                mountains++;
            }

            if (HasAnyValue(tags, "natural", "hill", "ridge") || HasKey(tags, "ele"))
            {
                hills++;
            }

            if (HasKey(tags, "military") || HasAnyValue(tags, "landuse", "military"))
            {
                militarySites++;
                specialFeatures++;
            }

            if (HasAnyValue(tags, "historic", "castle", "fort", "citywalls") ||
                HasAnyValue(tags, "building", "castle"))
            {
                castlesOrForts++;
                specialFeatures++;
            }

            if (HasAnyValue(tags, "office", "government") ||
                HasAnyValue(tags, "amenity", "townhall", "courthouse", "police", "fire_station"))
            {
                governmentSites++;
            }

            if (HasKey(tags, "waterway") ||
                HasAnyValue(tags, "natural", "water", "coastline") ||
                HasAnyValue(tags, "barrier", "retaining_wall", "city_wall"))
            {
                chokepoints++;
            }

            if (HasKey(tags, "building") || HasAnyValue(tags, "landuse", "residential", "commercial", "retail"))
            {
                urbanDensity++;
            }

            if (HasKey(tags, "highway"))
            {
                roads++;
            }

            if (HasKey(tags, "railway"))
            {
                railways++;
            }

            if (HasKey(tags, "bridge") || HasKey(tags, "tunnel"))
            {
                bridgesOrTunnels++;
            }

            if (HasKey(tags, "aeroway"))
            {
                airports++;
                specialFeatures++;
            }

            if (HasAnyValue(tags, "amenity", "ferry_terminal") ||
                HasAnyValue(tags, "harbour", "yes") ||
                HasAnyValue(tags, "seamark:type", "harbour", "marina") ||
                HasAnyValue(tags, "leisure", "marina"))
            {
                ports++;
                specialFeatures++;
            }

            if (HasAnyValue(tags, "amenity", "hospital", "university") ||
                HasAnyValue(tags, "power", "plant", "substation"))
            {
                specialFeatures++;
            }
        }

        return new TerritoryFeatureSummary(
            factories,
            shops,
            commercialAreas,
            offices,
            industrialSites,
            farmlandOrResources,
            populationSupport,
            mountains,
            hills,
            militarySites,
            castlesOrForts,
            governmentSites,
            chokepoints,
            urbanDensity,
            roads,
            railways,
            bridgesOrTunnels,
            airports,
            ports,
            connectionCount,
            areaSquareKm,
            specialFeatures);
    }

    private static bool HasKey(IReadOnlyDictionary<string, string> tags, string key) =>
        tags.ContainsKey(key);

    private static bool HasValue(IReadOnlyDictionary<string, string> tags, string key, string value) =>
        tags.TryGetValue(key, out var actual) &&
        string.Equals(actual, value, StringComparison.OrdinalIgnoreCase);

    private static bool HasAnyValue(IReadOnlyDictionary<string, string> tags, string key, params string[] values) =>
        tags.TryGetValue(key, out var actual) &&
        values.Any(value => string.Equals(actual, value, StringComparison.OrdinalIgnoreCase));
}
