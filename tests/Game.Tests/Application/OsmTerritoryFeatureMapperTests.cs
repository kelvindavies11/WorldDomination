using Game.Application;
using Game.Domain;

namespace Game.Tests.Application;

public sealed class OsmTerritoryFeatureMapperTests
{
    [Fact]
    public void MapsOsmTagsIntoTerritoryFeatureSummaryBuckets()
    {
        var summary = OsmTerritoryFeatureMapper.Map([
            new Dictionary<string, string>
            {
                ["shop"] = "supermarket",
                ["building"] = "industrial",
                ["highway"] = "primary",
                ["bridge"] = "yes"
            },
            new Dictionary<string, string>
            {
                ["railway"] = "station",
                ["amenity"] = "hospital",
                ["office"] = "government",
                ["historic"] = "castle"
            },
            new Dictionary<string, string>
            {
                ["aeroway"] = "aerodrome",
                ["natural"] = "peak",
                ["landuse"] = "commercial",
                ["waterway"] = "river"
            }
        ], areaSquareKm: 1.25, connectionCount: 4);

        Assert.Equal(1, summary.Shops);
        Assert.Equal(1, summary.Factories);
        Assert.Equal(1, summary.CommercialAreas);
        Assert.Equal(1, summary.Offices);
        Assert.Equal(1, summary.IndustrialSites);
        Assert.Equal(1, summary.PopulationSupport);
        Assert.Equal(1, summary.Mountains);
        Assert.Equal(1, summary.CastlesOrForts);
        Assert.Equal(1, summary.GovernmentSites);
        Assert.Equal(1, summary.Chokepoints);
        Assert.Equal(1, summary.Roads);
        Assert.Equal(1, summary.Railways);
        Assert.Equal(1, summary.BridgesOrTunnels);
        Assert.Equal(1, summary.Airports);
        Assert.Equal(4, summary.Connections);
        Assert.Equal(1.25, summary.AreaSquareKm);
        Assert.True(summary.SpecialFeatures >= 3);
    }
}
