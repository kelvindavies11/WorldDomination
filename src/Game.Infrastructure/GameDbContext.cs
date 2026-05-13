using Game.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Game.Infrastructure;

public sealed class GameDbContext(DbContextOptions<GameDbContext> options) : DbContext(options)
{
    public DbSet<GameEntity> Games => Set<GameEntity>();
    public DbSet<GamePlayerEntity> GamePlayers => Set<GamePlayerEntity>();
    public DbSet<MatchSnapshotEntity> MatchSnapshots => Set<MatchSnapshotEntity>();
    public DbSet<NpcTickCountEntity> NpcTickCounts => Set<NpcTickCountEntity>();
    public DbSet<TerritoryFeatureEntity> TerritoryFeatures => Set<TerritoryFeatureEntity>();
    public DbSet<PostcodeTerritoryEntity> PostcodeTerritories => Set<PostcodeTerritoryEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GameEntity>(e =>
        {
            e.HasKey(g => g.Id);
            e.HasMany(g => g.Players)
             .WithOne(p => p.Game)
             .HasForeignKey(p => p.GameId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GamePlayerEntity>(e =>
        {
            e.HasKey(p => new { p.GameId, p.PlayerId });
        });

        modelBuilder.Entity<MatchSnapshotEntity>(e =>
        {
            e.HasKey(s => s.GameId);
        });

        modelBuilder.Entity<NpcTickCountEntity>(e =>
        {
            e.HasKey(t => new { t.GameId, t.FactionId });
        });

        modelBuilder.Entity<TerritoryFeatureEntity>(e =>
        {
            e.HasKey(t => new { t.MapArea, t.TerritoryId });
            e.HasIndex(t => t.MapArea);
        });

        modelBuilder.Entity<PostcodeTerritoryEntity>(e =>
        {
            e.HasKey(p => new { p.MapArea, p.TerritoryId });
            e.HasIndex(p => p.MapArea);
        });
    }
}
