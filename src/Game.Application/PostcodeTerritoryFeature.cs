namespace Game.Application;

public sealed record PostcodeTerritoryFeature(
    string Postcode,
    string Name,
    IReadOnlyList<MapCoordinateDto> BoundaryCoordinates,
    double Latitude,
    double Longitude,
    string? Road);
