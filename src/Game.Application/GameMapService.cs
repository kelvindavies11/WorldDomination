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
                new MapCoordinateDto(-3.3300, 51.4050),
                new MapCoordinateDto(-3.0300, 51.5550)
            ],
            BoundaryCoordinates:
            [
                new MapCoordinateDto(-3.2820, 51.4300),
                new MapCoordinateDto(-3.2450, 51.4175),
                new MapCoordinateDto(-3.1960, 51.4120),
                new MapCoordinateDto(-3.1410, 51.4210),
                new MapCoordinateDto(-3.0910, 51.4440),
                new MapCoordinateDto(-3.0620, 51.4800),
                new MapCoordinateDto(-3.0740, 51.5150),
                new MapCoordinateDto(-3.1180, 51.5410),
                new MapCoordinateDto(-3.1810, 51.5480),
                new MapCoordinateDto(-3.2360, 51.5360),
                new MapCoordinateDto(-3.2860, 51.5060),
                new MapCoordinateDto(-3.3110, 51.4650),
                new MapCoordinateDto(-3.2820, 51.4300)
            ],
            Territories: [])
    ];

    public MatchMapDto GetMap(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return maps.Single(map => string.Equals(map.Id, id.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
