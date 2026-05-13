namespace Game.Domain;

public sealed record TerritoryStats(
    int Economy,
    int Defense,
    int Mobility,
    int StrategicValue,
    int RevenuePerTick,
    int ArmyGrowthPerTick);
