namespace Game.Infrastructure.Entities;

public sealed class NpcTickCountEntity
{
    public required string GameId { get; set; }
    public required string FactionId { get; set; }
    public int TickCount { get; set; }
}
