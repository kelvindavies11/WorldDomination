namespace Game.Domain;

public sealed record Ruleset(
    int FeatureCountCap,
    double TerritoryAreaCapSquareKm)
{
    public static Ruleset Default { get; } = new(
        FeatureCountCap: 10,
        TerritoryAreaCapSquareKm: 5);
}
