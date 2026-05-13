namespace Game.Infrastructure.Entities;

public sealed class GamePlayerEntity
{
    public required string GameId { get; set; }
    public required string PlayerId { get; set; }
    public required string FactionId { get; set; }
    public required string DisplayName { get; set; }
    public string? SelectedTerritoryId { get; set; }

    public GameEntity Game { get; set; } = null!;
}
