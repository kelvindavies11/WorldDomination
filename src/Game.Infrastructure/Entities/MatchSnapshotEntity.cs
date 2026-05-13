namespace Game.Infrastructure.Entities;

/// <summary>
/// Stores only the mutable portion of a running match.
/// Boundary coordinates and static territory stats are reconstructed from
/// <see cref="TerritoryFeatureEntity"/> and <see cref="PostcodeTerritoryEntity"/> on read.
/// </summary>
public sealed class MatchSnapshotEntity
{
    public required string GameId { get; set; }
    /// <summary>Canonical map ID, e.g. "cardiff". Used to join static territory tables.</summary>
    public required string MapId { get; set; }
    public required string MapArea { get; set; }
    public DateTimeOffset SnapshotGeneratedAtUtc { get; set; }

    /// <summary>JSON: serialized <c>MatchGameStateDto</c>.</summary>
    public required string GameStateJson { get; set; }

    /// <summary>JSON: <c>Dictionary&lt;string, string?&gt;</c> mapping territoryId → ownerFactionId (null = neutral).</summary>
    public required string TerritoryOwnersJson { get; set; }

    /// <summary>JSON: <c>List&lt;MatchArmyDto&gt;</c>.</summary>
    public required string ArmiesJson { get; set; }

    /// <summary>JSON: <c>List&lt;MatchFactionDto&gt;</c> — includes runtime display-name overrides.</summary>
    public required string FactionsJson { get; set; }

    /// <summary>JSON: <c>List&lt;MatchRouteDto&gt;</c> — static, computed once at match creation.</summary>
    public required string RoutesJson { get; set; }

    public DateTimeOffset? LastTerritoryMovementUtc { get; set; }
}
