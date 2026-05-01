using Game.Domain;

namespace Game.Tests.Domain;

public sealed class TerritoryStatCalculatorTests
{
    [Fact]
    public void CalculatesEconomyDefenseMobilityAndStrategicValueFromFeatureScores()
    {
        var features = new TerritoryFeatureSummary(
            Factories: 5,
            Shops: 8,
            CommercialAreas: 3,
            Offices: 4,
            IndustrialSites: 2,
            FarmlandOrResources: 1,
            PopulationSupport: 6,
            Mountains: 0,
            Hills: 3,
            MilitarySites: 1,
            CastlesOrForts: 1,
            GovernmentSites: 2,
            Chokepoints: 4,
            UrbanDensity: 7,
            Roads: 9,
            Railways: 3,
            BridgesOrTunnels: 4,
            Airports: 0,
            Ports: 1,
            Connections: 6,
            AreaSquareKm: 1.5,
            SpecialFeatures: 2);

        var stats = TerritoryStatCalculator.Calculate(features, Ruleset.Default);

        Assert.Equal(45, stats.Economy);
        Assert.Equal(21, stats.Defense);
        Assert.Equal(42, stats.Mobility);
        Assert.Equal(36, stats.StrategicValue);
    }

    [Fact]
    public void CapsDenseFeatureCountsAtOneHundred()
    {
        var features = TerritoryFeatureSummary.Empty with
        {
            Factories = 999,
            Shops = 999,
            CommercialAreas = 999,
            Offices = 999,
            IndustrialSites = 999,
            FarmlandOrResources = 999,
            PopulationSupport = 999
        };

        var stats = TerritoryStatCalculator.Calculate(features, Ruleset.Default);

        Assert.Equal(100, stats.Economy);
    }
}
