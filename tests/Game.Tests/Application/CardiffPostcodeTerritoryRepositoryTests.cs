using Game.Application;

namespace Game.Tests.Application;

public sealed class CardiffPostcodeTerritoryRepositoryTests
{
    [Fact]
    public void LoadsRealPostalSectorPolygonsFromGeoJson()
    {
        var geoJson = """
        {
          "type": "FeatureCollection",
          "features": [
            {
              "type": "Feature",
              "properties": {
                "postcodeSector": "CF10 1",
                "name": "CF10 1 - Castle",
                "locale": "Castle"
              },
              "geometry": {
                "type": "Polygon",
                "coordinates": [[
                  [-3.181, 51.480],
                  [-3.179, 51.480],
                  [-3.179, 51.482],
                  [-3.181, 51.482],
                  [-3.181, 51.480]
                ]]
              }
            }
          ]
        }
        """;

        var territories = CardiffPostcodeTerritoryRepository.LoadFromGeoJson(geoJson);

        var territory = Assert.Single(territories);
        Assert.Equal("CF10 1", territory.Postcode);
        Assert.Equal("CF10 1 - Castle", territory.Name);
        Assert.Equal("Castle", territory.Road);
        Assert.Equal(5, territory.BoundaryCoordinates.Count);
        Assert.Equal(territory.BoundaryCoordinates[0], territory.BoundaryCoordinates[^1]);
        Assert.Equal(-3.181, territory.BoundaryCoordinates[0].Longitude);
        Assert.Equal(51.480, territory.BoundaryCoordinates[0].Latitude);
    }

    [Fact]
    public void RejectsSectorPolygonsWithoutEnoughPoints()
    {
        var geoJson = """
        {
          "type": "FeatureCollection",
          "features": [
            {
              "type": "Feature",
              "properties": { "postcodeSector": "CF10 1" },
              "geometry": {
                "type": "Polygon",
                "coordinates": [[[-3.181, 51.480], [-3.179, 51.480]]]
              }
            }
          ]
        }
        """;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CardiffPostcodeTerritoryRepository.LoadFromGeoJson(geoJson));

        Assert.Contains("at least four", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
