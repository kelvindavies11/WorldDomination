using Game.Domain;

namespace Game.Tests.Domain;

public sealed class MovementCalculatorTests
{
    [Fact]
    public void CalculatesEtaFromDistanceTransportAndTerrain()
    {
        var route = new MovementRoute(
            BaseDistanceSeconds: 120,
            Transport: RouteTransport.Rail,
            Terrain: RouteTerrain.Mountain,
            Barrier: RouteBarrier.None);

        var result = MovementCalculator.Calculate(route);

        Assert.True(result.IsAllowed);
        Assert.Equal(84, result.EtaSeconds);
        Assert.Null(result.BlockedReason);
    }

    [Fact]
    public void BlocksInvalidWaterCrossings()
    {
        var route = new MovementRoute(
            BaseDistanceSeconds: 90,
            Transport: RouteTransport.Land,
            Terrain: RouteTerrain.Basic,
            Barrier: RouteBarrier.InvalidWaterCrossing);

        var result = MovementCalculator.Calculate(route);

        Assert.False(result.IsAllowed);
        Assert.Equal(0, result.EtaSeconds);
        Assert.Equal("Water or sea crossing requires a bridge, tunnel, port, or airport route.", result.BlockedReason);
    }

    [Fact]
    public void AllowsBridgeOrTunnelCrossings()
    {
        var route = new MovementRoute(
            BaseDistanceSeconds: 100,
            Transport: RouteTransport.Road,
            Terrain: RouteTerrain.Hills,
            Barrier: RouteBarrier.BridgeOrTunnel);

        var result = MovementCalculator.Calculate(route);

        Assert.True(result.IsAllowed);
        Assert.Equal(81, result.EtaSeconds);
    }
}
