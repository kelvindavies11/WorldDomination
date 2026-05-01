namespace Game.Domain;

public enum RouteTransport
{
    Land,
    Road,
    Rail,
    Air,
    Sea
}

public enum RouteTerrain
{
    Basic,
    Hills,
    Mountain
}

public enum RouteBarrier
{
    None,
    BridgeOrTunnel,
    InvalidWaterCrossing
}

public sealed record MovementRoute(
    int BaseDistanceSeconds,
    RouteTransport Transport,
    RouteTerrain Terrain,
    RouteBarrier Barrier);

public sealed record MovementResult(
    bool IsAllowed,
    int EtaSeconds,
    string? BlockedReason);
