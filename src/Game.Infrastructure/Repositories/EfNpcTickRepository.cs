using Game.Application;
using Microsoft.EntityFrameworkCore;

namespace Game.Infrastructure.Repositories;

public sealed class EfNpcTickRepository(GameDbContext db) : INpcTickRepository
{
    public int GetTickCount(string gameId, string factionId)
    {
        return db.NpcTickCounts
            .AsNoTracking()
            .Where(t => t.GameId == gameId && t.FactionId == factionId)
            .Select(t => t.TickCount)
            .FirstOrDefault();
    }

    public void SetTickCount(string gameId, string factionId, int count)
    {
        var existing = db.NpcTickCounts.Find(gameId, factionId);
        if (existing is null)
        {
            db.NpcTickCounts.Add(new Entities.NpcTickCountEntity
            {
                GameId = gameId,
                FactionId = factionId,
                TickCount = count
            });
        }
        else
        {
            existing.TickCount = count;
        }
        db.SaveChanges();
    }
}
