namespace Game.Application;

public interface IMatchStateRepository
{
    /// <summary>Returns the persisted mutable state only (no static territory data).</summary>
    MatchSnapshotMutableState? GetMutableState(string gameId);

    /// <summary>
    /// Loads the mutable state and reconstructs the full <see cref="MatchSnapshot"/>
    /// by joining the static territory and postcode tables. Returns null if no state exists.
    /// </summary>
    MatchSnapshot? GetFullSnapshot(string gameId);

    void SaveMutableState(string gameId, MatchSnapshotMutableState state);

    void DeleteMutableState(string gameId);

    DateTimeOffset? GetLastMovement(string gameId);

    void TrackMovement(string gameId, DateTimeOffset utc);
}

/// <summary>
/// The mutable portion of a match that changes each move. Stored in the DB.
/// Routes are static (computed once from territory boundaries) but stored here so
/// reconstruction does not need to recompute geographic adjacency.
/// </summary>
public sealed record MatchSnapshotMutableState(
    string GameId,
    /// <summary>Canonical map ID used for DB lookups, e.g. "cardiff", "wales-west".</summary>
    string MapId,
    /// <summary>Display name stored in the snapshot, e.g. "Cardiff &amp; Newport".</summary>
    string MapArea,
    DateTimeOffset SnapshotGeneratedAtUtc,
    MatchGameStateDto GameState,
    /// <summary>territoryId → ownerFactionId (null = neutral)</summary>
    IReadOnlyDictionary<string, string?> TerritoryOwners,
    IReadOnlyList<MatchArmyDto> Armies,
    /// <summary>Faction list including runtime display-name overrides.</summary>
    IReadOnlyList<MatchFactionDto> Factions,
    /// <summary>Static route graph — computed once at match creation, never changes.</summary>
    IReadOnlyList<MatchRouteDto> Routes);
