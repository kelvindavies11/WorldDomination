namespace Game.Infrastructure.Entities;

public sealed class GameEntity
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string MapArea { get; set; }
    public int MaxHumanPlayers { get; set; }
    public int NpcFactions { get; set; }
    public int TerritoryCount { get; set; }
    public double WinningControlPercentage { get; set; } = 100;
    public bool IsStarted { get; set; }
    public bool IsEnded { get; set; }
    public string? WinnerFactionId { get; set; }
    public string? WinnerFactionName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }

    public List<GamePlayerEntity> Players { get; set; } = [];
}
