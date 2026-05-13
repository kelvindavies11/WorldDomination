using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Application;
using Game.Domain;
using Game.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Game.Infrastructure.Repositories;

public sealed class EfMatchStateRepository(GameDbContext db, GameMapService mapService) : IMatchStateRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public MatchSnapshotMutableState? GetMutableState(string gameId)
    {
        var entity = db.MatchSnapshots
            .AsNoTracking()
            .FirstOrDefault(s => s.GameId == gameId.Trim());

        return entity is null ? null : Deserialize(entity);
    }

    public MatchSnapshot? GetFullSnapshot(string gameId)
    {
        var entity = db.MatchSnapshots
            .AsNoTracking()
            .FirstOrDefault(s => s.GameId == gameId.Trim());

        if (entity is null) return null;

        var postcodes = db.PostcodeTerritories
            .AsNoTracking()
            .Where(p => p.MapArea == entity.MapId)
            .ToList();

        var features = db.TerritoryFeatures
            .AsNoTracking()
            .Where(f => f.MapArea == entity.MapId)
            .ToDictionary(f => f.TerritoryId, StringComparer.OrdinalIgnoreCase);

        var map = mapService.GetMap(entity.MapId);
        var state = Deserialize(entity);
        return ReconstructSnapshot(state, map, postcodes, features);
    }

    public void SaveMutableState(string gameId, MatchSnapshotMutableState state)
    {
        var normalizedId = gameId.Trim();
        var existing = db.MatchSnapshots.Find(normalizedId);
        if (existing is null)
        {
            db.MatchSnapshots.Add(Serialize(normalizedId, state));
        }
        else
        {
            existing.MapId = state.MapId;
            existing.MapArea = state.MapArea;
            existing.SnapshotGeneratedAtUtc = state.SnapshotGeneratedAtUtc;
            existing.GameStateJson = JsonSerializer.Serialize(state.GameState, JsonOptions);
            existing.TerritoryOwnersJson = JsonSerializer.Serialize(state.TerritoryOwners, JsonOptions);
            existing.ArmiesJson = JsonSerializer.Serialize(state.Armies, JsonOptions);
            existing.FactionsJson = JsonSerializer.Serialize(state.Factions, JsonOptions);
            existing.RoutesJson = JsonSerializer.Serialize(state.Routes, JsonOptions);
        }
        db.SaveChanges();
    }

    public void DeleteMutableState(string gameId)
    {
        var entity = db.MatchSnapshots.Find(gameId.Trim());
        if (entity is not null)
        {
            db.MatchSnapshots.Remove(entity);
            db.SaveChanges();
        }
    }

    public DateTimeOffset? GetLastMovement(string gameId)
    {
        return db.MatchSnapshots
            .AsNoTracking()
            .Where(s => s.GameId == gameId.Trim())
            .Select(s => s.LastTerritoryMovementUtc)
            .FirstOrDefault();
    }

    public void TrackMovement(string gameId, DateTimeOffset utc)
    {
        var entity = db.MatchSnapshots.Find(gameId.Trim());
        if (entity is not null)
        {
            entity.LastTerritoryMovementUtc = utc;
            db.SaveChanges();
        }
    }

    // ── Reconstruction ───────────────────────────────────────────────────────

    public MatchSnapshot ReconstructSnapshot(
        MatchSnapshotMutableState state,
        MatchMapDto map,
        IReadOnlyList<PostcodeTerritoryEntity> postcodes,
        IReadOnlyDictionary<string, TerritoryFeatureEntity> features)
    {
        var territories = postcodes
            .OrderBy(p => p.TerritoryId, StringComparer.OrdinalIgnoreCase)
            .Select((p, index) =>
            {
                features.TryGetValue(p.TerritoryId, out var feat);
                var stats = feat is null
                    ? new TerritoryStats(0, 0, 0, 0, 0, 0)
                    : new TerritoryStats(feat.StatsEconomy, feat.StatsDefense, feat.StatsMobility,
                        feat.StatsStrategicValue, feat.StatsRevenuePerTick, feat.StatsArmyGrowthPerTick);
                var featureSummary = feat is null
                    ? TerritoryFeatureSummary.Empty
                    : new TerritoryFeatureSummary(
                        feat.Factories, feat.Shops, feat.CommercialAreas, feat.Offices, feat.IndustrialSites,
                        feat.FarmlandOrResources, feat.PopulationSupport, feat.Mountains, feat.Hills,
                        feat.MilitarySites, feat.CastlesOrForts, feat.GovernmentSites, feat.Chokepoints,
                        feat.UrbanDensity, feat.Roads, feat.Railways, feat.BridgesOrTunnels, feat.Airports,
                        feat.Ports, feat.Connections, feat.AreaSquareKm, feat.SpecialFeatures);
                state.TerritoryOwners.TryGetValue(p.TerritoryId, out var owner);
                var boundary = JsonSerializer.Deserialize<IReadOnlyList<MapCoordinateDto>>(
                    p.BoundaryCoordinatesJson, JsonOptions) ?? [];
                return new MatchTerritoryDto(
                    Id: p.TerritoryId,
                    Index: index,
                    Name: p.Name,
                    AreaSquareKm: feat?.AreaSquareKm ?? 0,
                    OwnerFactionId: owner,
                    Stats: stats,
                    Postcode: feat?.Postcode,
                    Features: featureSummary,
                    BoundaryCoordinates: boundary);
            })
            .ToList();

        var resources = state.Factions
            .Select(f => new MatchFactionResourceDto(
                f.Id,
                territories.Where(t => t.OwnerFactionId == f.Id).Sum(t => t.Stats.Economy)))
            .ToList();

        var leaderboard = MapControlCalculator.CalculateLeaderboard(
            territories.Select(t => new ControlledTerritory(t.Id, t.OwnerFactionId, t.AreaSquareKm, t.Stats)).ToArray(),
            state.Factions.Select(f => new FactionStanding(f.Id, f.Name, EliminationCount: 0)).ToArray(),
            state.Armies.Select(a => new ControlledArmy(a.FactionId, a.Strength)).ToArray(),
            state.Routes.Select(r => new ConnectedRoute(r.SourceTerritoryId, r.DestinationTerritoryId, r.IsAllowed)).ToArray(),
            resources.ToDictionary(r => r.FactionId, r => r.Revenue, StringComparer.Ordinal));

        return new MatchSnapshot(
            GameId: state.GameId,
            MapArea: state.MapArea,
            SnapshotGeneratedAtUtc: state.SnapshotGeneratedAtUtc,
            Game: state.GameState,
            Map: map,
            Factions: state.Factions,
            Territories: territories,
            Armies: state.Armies,
            Routes: state.Routes,
            Leaderboard: leaderboard,
            Resources: resources);
    }

    // ── Serialization helpers ─────────────────────────────────────────────────

    private static MatchSnapshotEntity Serialize(string gameId, MatchSnapshotMutableState state) => new()
    {
        GameId = gameId,
        MapId = state.MapId,
        MapArea = state.MapArea,
        SnapshotGeneratedAtUtc = state.SnapshotGeneratedAtUtc,
        GameStateJson = JsonSerializer.Serialize(state.GameState, JsonOptions),
        TerritoryOwnersJson = JsonSerializer.Serialize(state.TerritoryOwners, JsonOptions),
        ArmiesJson = JsonSerializer.Serialize(state.Armies, JsonOptions),
        FactionsJson = JsonSerializer.Serialize(state.Factions, JsonOptions),
        RoutesJson = JsonSerializer.Serialize(state.Routes, JsonOptions)
    };

    private static MatchSnapshotMutableState Deserialize(MatchSnapshotEntity e) => new(
        GameId: e.GameId,
        MapId: e.MapId,
        MapArea: e.MapArea,
        SnapshotGeneratedAtUtc: e.SnapshotGeneratedAtUtc,
        GameState: JsonSerializer.Deserialize<MatchGameStateDto>(e.GameStateJson, JsonOptions)!,
        TerritoryOwners: JsonSerializer.Deserialize<Dictionary<string, string?>>(e.TerritoryOwnersJson, JsonOptions)!,
        Armies: JsonSerializer.Deserialize<List<MatchArmyDto>>(e.ArmiesJson, JsonOptions)!,
        Factions: JsonSerializer.Deserialize<List<MatchFactionDto>>(e.FactionsJson, JsonOptions)!,
        Routes: JsonSerializer.Deserialize<List<MatchRouteDto>>(e.RoutesJson, JsonOptions)!);
}
