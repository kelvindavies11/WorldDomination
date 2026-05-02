namespace Game.Application;

public sealed class GameMapService
{
    private readonly List<MatchMapDto> maps =
    [
        new(
            Id: "cardiff",
            Name: "Cardiff",
            Center: new MapCoordinateDto(-3.1791, 51.4816),
            CameraBounds:
            [
                new MapCoordinateDto(-3.4050, 51.3700),
                new MapCoordinateDto(-3.0300, 51.5850)
            ],
            BoundaryCoordinates:
            [
                new MapCoordinateDto(-3.3850, 51.3860),
                new MapCoordinateDto(-3.3440, 51.3750),
                new MapCoordinateDto(-3.2820, 51.3820),
                new MapCoordinateDto(-3.2140, 51.3920),
                new MapCoordinateDto(-3.1450, 51.4140),
                new MapCoordinateDto(-3.0910, 51.4440),
                new MapCoordinateDto(-3.0620, 51.4800),
                new MapCoordinateDto(-3.0740, 51.5150),
                new MapCoordinateDto(-3.1180, 51.5410),
                new MapCoordinateDto(-3.1810, 51.5700),
                new MapCoordinateDto(-3.2550, 51.5720),
                new MapCoordinateDto(-3.3370, 51.5480),
                new MapCoordinateDto(-3.3890, 51.4940),
                new MapCoordinateDto(-3.4050, 51.4320),
                new MapCoordinateDto(-3.3850, 51.3860)
            ],
            Territories: [])
    ];

    public MatchMapDto GetMap(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return maps.Single(map => string.Equals(map.Id, id.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
