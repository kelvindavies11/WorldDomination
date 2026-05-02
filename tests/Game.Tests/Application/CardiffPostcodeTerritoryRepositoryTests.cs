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
        Assert.Equal(5, territory.BoundaryCoordinates.Count);
        Assert.Equal(territory.BoundaryCoordinates[0], territory.BoundaryCoordinates[^1]);
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
