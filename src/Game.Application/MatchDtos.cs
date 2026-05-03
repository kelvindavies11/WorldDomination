using Game.Domain;

namespace Game.Application;

public enum FactionKind
{
    Human,
    Npc
}

public sealed record MatchSnapshot(
    string GameId,
    string MapArea,
    DateTimeOffset SnapshotGeneratedAtUtc,
    MatchMapDto Map,
    IReadOnlyList<MatchFactionDto> Factions,
    IReadOnlyList<MatchTerritoryDto> Territories,
    IReadOnlyList<MatchArmyDto> Armies,
    IReadOnlyList<MatchRouteDto> Routes,
    IReadOnlyList<LeaderboardRow> Leaderboard);

public sealed record MatchMapDto(
    string Id,
    string Name,
    MapCoordinateDto Center,
    IReadOnlyList<MapCoordinateDto> CameraBounds,
    IReadOnlyList<MapCoordinateDto> BoundaryCoordinates);

public sealed record MapCoordinateDto(
    double Longitude,
    double Latitude);

public sealed record MatchFactionDto(
    string Id,
    string Name,
    FactionKind Kind,
    string Color);

public sealed record MatchTerritoryDto(
    string Id,
    int Index,
    string Name,
    double AreaSquareKm,
    string? OwnerFactionId,
    TerritoryStats Stats,
    string? Postcode,
    TerritoryFeatureSummary Features,
    IReadOnlyList<MapCoordinateDto> BoundaryCoordinates);

public sealed record MatchArmyDto(
    string Id,
    string FactionId,
    string TerritoryId,
    int Strength);

public sealed record MatchRouteDto(
    string SourceTerritoryId,
    string DestinationTerritoryId,
    RouteTransport Transport,
    int EtaSeconds,
    bool IsAllowed);
