using Game.Application;

namespace Game.Tests.Application;

public sealed class CardiffPostcodeTerritoryRepositoryTests
{
    [Fact]
    public void LoadsPostcodesAndCreatesClosedStreetLevelPolygons()
    {
        var csv = """
        "Postcode","Latitude","Longitude","District","Ward","Roads","Population","Households"
        "CF10 1AA","51.479103","-3.178094","Cardiff","Cathays","Heol Eglwys Fair","",""
        """;

        var territories = CardiffPostcodeTerritoryRepository.LoadFromCsv(csv);

        var territory = Assert.Single(territories);
        Assert.Equal("CF10 1AA", territory.Postcode);
        Assert.Equal("CF10 1AA - Heol Eglwys Fair", territory.Name);
        Assert.InRange(territory.BoundaryCoordinates.Count, 7, 10);
        Assert.Equal(territory.BoundaryCoordinates[0], territory.BoundaryCoordinates[^1]);
        Assert.True(territory.BoundaryCoordinates.Select(point => point.Longitude).Distinct().Count() >= 3);
        Assert.True(territory.BoundaryCoordinates.Select(point => point.Latitude).Distinct().Count() >= 3);
    }

    [Fact]
    public void CreatesStableDifferentShapesForDifferentPostcodes()
    {
        var csv = """
        "Postcode","Latitude","Longitude","District","Ward","Roads","Population","Households"
        "CF10 1AA","51.479103","-3.178094","Cardiff","Cathays","Heol Eglwys Fair","",""
        "CF10 1AB","51.478637","-3.177909","Cardiff","Cathays","Heol Eglwys Fair","",""
        """;

        var firstLoad = CardiffPostcodeTerritoryRepository.LoadFromCsv(csv);
        var secondLoad = CardiffPostcodeTerritoryRepository.LoadFromCsv(csv);

        Assert.Equal(firstLoad[0].BoundaryCoordinates, secondLoad[0].BoundaryCoordinates);
        Assert.NotEqual(firstLoad[0].BoundaryCoordinates, firstLoad[1].BoundaryCoordinates);
    }

    [Fact]
    public void CreatesSharedEdgesBetweenAdjacentGeneratedTerritories()
    {
        var csv = """
        "Postcode","Latitude","Longitude","District","Ward","Roads","Population","Households"
        "CF10 1AA","51.480000","-3.180000","Cardiff","Cathays","Street A","",""
        "CF10 1AB","51.480000","-3.179000","Cardiff","Cathays","Street B","",""
        "CF10 1AC","51.479000","-3.180000","Cardiff","Cathays","Street C","",""
        "CF10 1AD","51.479000","-3.179000","Cardiff","Cathays","Street D","",""
        """;

        var territories = CardiffPostcodeTerritoryRepository.LoadFromCsv(csv);

        Assert.Contains(territories[0].BoundaryCoordinates, point => territories[1].BoundaryCoordinates.Contains(point));
        Assert.Contains(territories[0].BoundaryCoordinates, point => territories[2].BoundaryCoordinates.Contains(point));
    }

    [Fact]
    public void RejectsRowsWithoutCoordinates()
    {
        var csv = """
        "Postcode","Latitude","Longitude","District","Ward","Roads","Population","Households"
        "CF10 1AA","","-3.178094","Cardiff","Cathays","Heol Eglwys Fair","",""
        """;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CardiffPostcodeTerritoryRepository.LoadFromCsv(csv));

        Assert.Contains("coordinates", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
