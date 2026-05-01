using Game.Domain;

namespace Game.Application;

public enum FactionKind
{
    Human,
    Npc
}

public sealed record PrototypeMatchSnapshot(
    string GameId,
    string MapArea,
    IReadOnlyList<PrototypeFactionDto> Factions,
    IReadOnlyList<PrototypeTerritoryDto> Territories,
    IReadOnlyList<PrototypeArmyDto> Armies,
    IReadOnlyList<PrototypeRouteDto> Routes,
    IReadOnlyList<LeaderboardRow> Leaderboard);

public sealed record PrototypeFactionDto(
    string Id,
    string Name,
    FactionKind Kind,
    string Color);

public sealed record PrototypeTerritoryDto(
    string Id,
    int Index,
    string Name,
    double AreaSquareKm,
    string? OwnerFactionId,
    TerritoryStats Stats);

public sealed record PrototypeArmyDto(
    string Id,
    string FactionId,
    string TerritoryId,
    int Strength);

public sealed record PrototypeRouteDto(
    string SourceTerritoryId,
    string DestinationTerritoryId,
    RouteTransport Transport,
    int EtaSeconds,
    bool IsAllowed);
