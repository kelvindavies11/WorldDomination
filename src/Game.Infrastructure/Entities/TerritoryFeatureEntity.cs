namespace Game.Infrastructure.Entities;

/// <summary>
/// Static territory feature data seeded from OSM JSON files.
/// Composite PK: (MapArea, TerritoryId).
/// </summary>
public sealed class TerritoryFeatureEntity
{
    public required string MapArea { get; set; }
    public required string TerritoryId { get; set; }
    public required string Name { get; set; }
    public string? Postcode { get; set; }
    public double AreaSquareKm { get; set; }

    // TerritoryFeatureSummary fields
    public int Factories { get; set; }
    public int Shops { get; set; }
    public int CommercialAreas { get; set; }
    public int Offices { get; set; }
    public int IndustrialSites { get; set; }
    public int FarmlandOrResources { get; set; }
    public int PopulationSupport { get; set; }
    public int Mountains { get; set; }
    public int Hills { get; set; }
    public int MilitarySites { get; set; }
    public int CastlesOrForts { get; set; }
    public int GovernmentSites { get; set; }
    public int Chokepoints { get; set; }
    public int UrbanDensity { get; set; }
    public int Roads { get; set; }
    public int Railways { get; set; }
    public int BridgesOrTunnels { get; set; }
    public int Airports { get; set; }
    public int Ports { get; set; }
    public int Connections { get; set; }
    public int SpecialFeatures { get; set; }

    // TerritoryStats fields (derived — stored so we don't recalculate every read)
    public int StatsEconomy { get; set; }
    public int StatsDefense { get; set; }
    public int StatsMobility { get; set; }
    public int StatsStrategicValue { get; set; }
    public int StatsRevenuePerTick { get; set; }
    public int StatsArmyGrowthPerTick { get; set; }
}
