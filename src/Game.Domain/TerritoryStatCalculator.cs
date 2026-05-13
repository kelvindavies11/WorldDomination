namespace Game.Domain;

public static class TerritoryStatCalculator
{
    public static TerritoryStats Calculate(TerritoryFeatureSummary features, Ruleset ruleset)
    {
        var economy = Round(
            0.25 * CappedScore(features.Factories, ruleset.FeatureCountCap) +
            0.20 * CappedScore(features.Shops, ruleset.FeatureCountCap) +
            0.15 * CappedScore(features.CommercialAreas, ruleset.FeatureCountCap) +
            0.15 * CappedScore(features.Offices, ruleset.FeatureCountCap) +
            0.10 * CappedScore(features.IndustrialSites, ruleset.FeatureCountCap) +
            0.10 * CappedScore(features.FarmlandOrResources, ruleset.FeatureCountCap) +
            0.05 * CappedScore(features.PopulationSupport, ruleset.FeatureCountCap));

        var defense = Round(
            0.25 * CappedScore(features.Mountains, ruleset.FeatureCountCap) +
            0.15 * CappedScore(features.Hills, ruleset.FeatureCountCap) +
            0.15 * CappedScore(features.MilitarySites, ruleset.FeatureCountCap) +
            0.15 * CappedScore(features.CastlesOrForts, ruleset.FeatureCountCap) +
            0.10 * CappedScore(features.GovernmentSites, ruleset.FeatureCountCap) +
            0.10 * CappedScore(features.Chokepoints, ruleset.FeatureCountCap) +
            0.10 * CappedScore(features.UrbanDensity, ruleset.FeatureCountCap));

        var mobility = Round(
            0.25 * CappedScore(features.Roads, ruleset.FeatureCountCap) +
            0.20 * CappedScore(features.Railways, ruleset.FeatureCountCap) +
            0.15 * CappedScore(features.BridgesOrTunnels, ruleset.FeatureCountCap) +
            0.15 * CappedScore(features.Airports, ruleset.FeatureCountCap) +
            0.15 * CappedScore(features.Ports, ruleset.FeatureCountCap) +
            0.10 * CappedScore(features.Connections, ruleset.FeatureCountCap));

        var strategicValue = Round(
            0.35 * economy +
            0.25 * defense +
            0.25 * mobility +
            0.10 * CappedScore(features.AreaSquareKm, ruleset.TerritoryAreaCapSquareKm) +
            0.05 * CappedScore(features.SpecialFeatures, ruleset.FeatureCountCap));

        var revenuePerTick = Round(economy * 10 + strategicValue * 3);
        var armyGrowthPerTick = Math.Max(1, Round(
            economy * 0.08 +
            strategicValue * 0.04 +
            mobility * 0.03));

        return new TerritoryStats(
            economy,
            defense,
            mobility,
            strategicValue,
            revenuePerTick,
            armyGrowthPerTick);
    }

    public static double CappedScore(double value, double cap)
    {
        if (cap <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cap), "Cap must be greater than zero.");
        }

        return Math.Min(Math.Max(value, 0), cap) / cap * 100;
    }

    private static int Round(double value) =>
        (int)Math.Round(value, MidpointRounding.AwayFromZero);
}
