namespace Game.Infrastructure.Entities;

/// <summary>
/// Static postcode territory boundary data seeded from GeoJSON files.
/// Composite PK: (MapArea, TerritoryId).
/// </summary>
public sealed class PostcodeTerritoryEntity
{
    public required string MapArea { get; set; }

    /// <summary>Postcode sector, e.g. "CF10 1".</summary>
    public required string TerritoryId { get; set; }

    public required string Name { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? Road { get; set; }

    /// <summary>JSON: <c>IReadOnlyList&lt;MapCoordinateDto&gt;</c> polygon boundary.</summary>
    public required string BoundaryCoordinatesJson { get; set; }
}
