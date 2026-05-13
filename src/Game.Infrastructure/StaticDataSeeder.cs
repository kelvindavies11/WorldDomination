using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Application;
using Game.Domain;
using Game.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Game.Infrastructure;

public static class StaticDataSeeder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<GameDbContext>>();

        var mapsToSeed = new[]
        {
            new MapSeedConfig(
                MapArea: "cardiff",
                LoadPostcodes: () => new CardiffPostcodeTerritoryRepository().Load(),
                LoadFeatures: () => new CardiffTerritoryFeatureRepository().Load()),
            new MapSeedConfig(
                MapArea: "wales-west",
                LoadPostcodes: () => new WalesWestPostcodeTerritoryRepository().Load(),
                LoadFeatures: () => new WalesWestTerritoryFeatureRepository().Load()),
            new MapSeedConfig(
                MapArea: "north-wales",
                LoadPostcodes: () => new NorthWalesPostcodeTerritoryRepository().Load(),
                LoadFeatures: () => new WalesTerritoryFeatureRepository().Load()),
            new MapSeedConfig(
                MapArea: "mid-wales",
                LoadPostcodes: () => new MidWalesPostcodeTerritoryRepository().Load(),
                LoadFeatures: () => new WalesTerritoryFeatureRepository().Load()),
            new MapSeedConfig(
                MapArea: "south-wales",
                LoadPostcodes: () => new SouthWalesPostcodeTerritoryRepository().Load(),
                LoadFeatures: () => new WalesTerritoryFeatureRepository().Load()),
        };

        foreach (var map in mapsToSeed)
        {
            await SeedMapAsync(db, logger, map).ConfigureAwait(false);
        }
    }

    private static async Task SeedMapAsync(GameDbContext db, ILogger logger, MapSeedConfig config)
    {
        // Skip if already seeded — check for any row with this MapArea
        var alreadySeeded = await db.PostcodeTerritories
            .AnyAsync(p => p.MapArea == config.MapArea)
            .ConfigureAwait(false);

        if (alreadySeeded)
        {
            return;
        }

        logger.LogInformation("Seeding static data for map area: {MapArea}", config.MapArea);

        IReadOnlyList<PostcodeTerritoryFeature> postcodes;
        IReadOnlyDictionary<string, TerritoryFeatureSummary> features;
        try
        {
            postcodes = config.LoadPostcodes();
            features = config.LoadFeatures();
        }
        catch (FileNotFoundException ex)
        {
            logger.LogWarning(ex, "Static data file not found for {MapArea}; skipping seed.", config.MapArea);
            return;
        }

        var postcodesEntities = postcodes.Select(p => new PostcodeTerritoryEntity
        {
            MapArea = config.MapArea,
            TerritoryId = NormalizeTerritoryId(p.Postcode),
            Name = p.Name,
            Latitude = p.Latitude,
            Longitude = p.Longitude,
            Road = p.Road,
            BoundaryCoordinatesJson = JsonSerializer.Serialize(p.BoundaryCoordinates, JsonOptions)
        }).ToList();

        var featureEntities = postcodes.Select(p =>
        {
            features.TryGetValue(p.Postcode, out var summary);
            summary ??= TerritoryFeatureSummary.Empty;
            var stats = TerritoryStatCalculator.Calculate(summary, Ruleset.Default);
            return new TerritoryFeatureEntity
            {
                MapArea = config.MapArea,
                TerritoryId = NormalizeTerritoryId(p.Postcode),
                Name = p.Name,
                Postcode = p.Postcode,
                AreaSquareKm = summary.AreaSquareKm,
                Factories = summary.Factories,
                Shops = summary.Shops,
                CommercialAreas = summary.CommercialAreas,
                Offices = summary.Offices,
                IndustrialSites = summary.IndustrialSites,
                FarmlandOrResources = summary.FarmlandOrResources,
                PopulationSupport = summary.PopulationSupport,
                Mountains = summary.Mountains,
                Hills = summary.Hills,
                MilitarySites = summary.MilitarySites,
                CastlesOrForts = summary.CastlesOrForts,
                GovernmentSites = summary.GovernmentSites,
                Chokepoints = summary.Chokepoints,
                UrbanDensity = summary.UrbanDensity,
                Roads = summary.Roads,
                Railways = summary.Railways,
                BridgesOrTunnels = summary.BridgesOrTunnels,
                Airports = summary.Airports,
                Ports = summary.Ports,
                Connections = summary.Connections,
                SpecialFeatures = summary.SpecialFeatures,
                StatsEconomy = stats.Economy,
                StatsDefense = stats.Defense,
                StatsMobility = stats.Mobility,
                StatsStrategicValue = stats.StrategicValue,
                StatsRevenuePerTick = stats.RevenuePerTick,
                StatsArmyGrowthPerTick = stats.ArmyGrowthPerTick
            };
        }).ToList();

        await db.PostcodeTerritories.AddRangeAsync(postcodesEntities).ConfigureAwait(false);
        await db.TerritoryFeatures.AddRangeAsync(featureEntities).ConfigureAwait(false);

        try
        {
            await db.SaveChangesAsync().ConfigureAwait(false);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
            when (ex.InnerException?.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase) == true)
        {
            // Another process seeded concurrently — the data is already there, continue
            db.ChangeTracker.Clear();
            logger.LogInformation("Seed for {MapArea} was applied by a concurrent process; skipping.", config.MapArea);
            return;
        }

        logger.LogInformation("Seeded {PostcodeCount} territories for {MapArea}.", postcodesEntities.Count, config.MapArea);
    }

    private static string NormalizeTerritoryId(string postcode) =>
        "postcode-" + postcode.Trim().ToLowerInvariant().Replace(" ", "-", StringComparison.Ordinal);

    private sealed record MapSeedConfig(
        string MapArea,
        Func<IReadOnlyList<PostcodeTerritoryFeature>> LoadPostcodes,
        Func<IReadOnlyDictionary<string, TerritoryFeatureSummary>> LoadFeatures);
}
