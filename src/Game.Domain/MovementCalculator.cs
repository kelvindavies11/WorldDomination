namespace Game.Domain;

public static class MovementCalculator
{
    public static MovementResult Calculate(MovementRoute route)
    {
        if (route.BaseDistanceSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(route), "Base distance seconds cannot be negative.");
        }

        if (route.Barrier == RouteBarrier.InvalidWaterCrossing)
        {
            return new MovementResult(
                IsAllowed: false,
                EtaSeconds: 0,
                BlockedReason: "Water or sea crossing requires a bridge, tunnel, port, or airport route.");
        }

        var eta = route.BaseDistanceSeconds *
            TerrainMultiplier(route.Terrain) *
            BarrierMultiplier(route.Barrier) *
            TransportMultiplier(route.Transport);

        return new MovementResult(
            IsAllowed: true,
            EtaSeconds: (int)Math.Round(eta, MidpointRounding.AwayFromZero),
            BlockedReason: null);
    }

    private static decimal TransportMultiplier(RouteTransport transport) =>
        transport switch
        {
            RouteTransport.Land => 1.00m,
            RouteTransport.Road => 0.70m,
            RouteTransport.Rail => 0.50m,
            RouteTransport.Air => 0.35m,
            RouteTransport.Sea => 0.60m,
            _ => throw new ArgumentOutOfRangeException(nameof(transport), transport, null)
        };

    private static decimal TerrainMultiplier(RouteTerrain terrain) =>
        terrain switch
        {
            RouteTerrain.Basic => 1.00m,
            RouteTerrain.Hills => 1.15m,
            RouteTerrain.Mountain => 1.40m,
            _ => throw new ArgumentOutOfRangeException(nameof(terrain), terrain, null)
        };

    private static decimal BarrierMultiplier(RouteBarrier barrier) =>
        barrier switch
        {
            RouteBarrier.None => 1.00m,
            RouteBarrier.BridgeOrTunnel => 1.00m,
            RouteBarrier.InvalidWaterCrossing => 0.00m,
            _ => throw new ArgumentOutOfRangeException(nameof(barrier), barrier, null)
        };
}
