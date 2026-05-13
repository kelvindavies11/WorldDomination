using Game.Domain;

namespace Game.Application;

public enum FactionKind
{
    Human,
    Npc
}

public enum NpcNature
{
    Active,
    Conservative,
    Passive
}

public sealed record MatchSnapshot(
    string GameId,
    string MapArea,
    DateTimeOffset SnapshotGeneratedAtUtc,
    MatchGameStateDto Game,
    MatchMapDto Map,
    IReadOnlyList<MatchFactionDto> Factions,
    IReadOnlyList<MatchTerritoryDto> Territories,
    IReadOnlyList<MatchArmyDto> Armies,
    IReadOnlyList<MatchRouteDto> Routes,
    IReadOnlyList<LeaderboardRow> Leaderboard,
    IReadOnlyList<MatchFactionResourceDto>? Resources = null);

public sealed record MatchGameStateDto(
    string Status,
    bool IsStarted,
    int HumanPlayers,
    int MaxHumanPlayers,
    int NpcFactions,
    bool IsEnded = false,
    double WinningControlPercentage = 100,
    string? WinnerFactionId = null,
    string? WinnerFactionName = null);

public sealed record MatchFactionResourceDto(
    string FactionId,
    int Revenue);

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
    string Color,
    NpcNature? Nature = null);

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

public sealed record SelectStartPositionRequest(
    string TerritoryId);

public sealed record JoinGameResponse(
    string GameId,
    string FactionId,
    string DisplayName,
    string Status,
    int HumanPlayers,
    int MaxHumanPlayers);

public sealed record SendArmyCommand(
    string PlayerFactionId,
    string SourceTerritoryId,
    string TargetTerritoryId,
    int Strength,
    string GameId = "cardiff-match");

public sealed record SendArmyResult(
    bool Accepted,
    string? Error,
    int? EtaSeconds,
    MatchSnapshot? Snapshot,
    string? EliminatedFactionName = null);
