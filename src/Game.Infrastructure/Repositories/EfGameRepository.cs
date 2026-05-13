using Game.Application;
using Game.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Game.Infrastructure.Repositories;

public sealed class EfGameRepository(GameDbContext db) : IGameRepository
{
    public IReadOnlyList<AvailableGameDto> ListAvailableGames() =>
        db.Games
            .Include(g => g.Players)
            .AsNoTracking()
            .AsEnumerable()
            .Select(ToAvailableGame)
            .OrderBy(g => g.Status == "Open" ? 0 : g.Status == "Started" ? 1 : 2)
            .ThenByDescending(g => g.Status == "Started" ? g.StartedAt : g.Status == "Ended" ? g.EndedAt : g.CreatedAt)
            .ToList();

    public LobbyGameState? GetById(string gameId) =>
        db.Games
            .Include(g => g.Players)
            .AsNoTracking()
            .Where(g => g.Id == gameId.Trim())
            .AsEnumerable()
            .Select(ToLobbyGameState)
            .FirstOrDefault();

    public MatchSetupOptions? GetMatchSetup(string gameId)
    {
        var entity = db.Games
            .Include(g => g.Players)
            .AsNoTracking()
            .FirstOrDefault(g => g.Id == gameId.Trim());

        return entity is null ? null : ToMatchSetupOptions(entity);
    }

    public int GetMaxGameNumber()
    {
        // Game IDs are formatted as "game-001", "game-002", etc.
        var ids = db.Games.AsNoTracking().Select(g => g.Id).ToList();
        if (ids.Count == 0) return 0;

        return ids
            .Select(id => int.TryParse(id.Replace("game-", "", StringComparison.OrdinalIgnoreCase), out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();
    }

    public void Add(LobbyGameState game)
    {
        db.Games.Add(ToEntity(game));
        db.SaveChanges();
    }

    public void Update(LobbyGameState game)
    {
        var entity = db.Games.Find(game.Id);
        if (entity is null) return;

        entity.Name = game.Name;
        entity.IsStarted = game.IsStarted;
        entity.IsEnded = game.IsEnded;
        entity.WinnerFactionId = game.WinnerFactionId;
        entity.WinnerFactionName = game.WinnerFactionName;
        entity.StartedAt = game.StartedAt;
        entity.EndedAt = game.EndedAt;
        entity.WinningControlPercentage = game.WinningControlPercentage;
        db.SaveChanges();
    }

    public void AddPlayer(string gameId, LobbyPlayerState player)
    {
        db.GamePlayers.Add(new GamePlayerEntity
        {
            GameId = gameId.Trim(),
            PlayerId = player.PlayerId,
            FactionId = player.FactionId,
            DisplayName = player.DisplayName,
            SelectedTerritoryId = player.SelectedTerritoryId
        });
        db.SaveChanges();
    }

    public void UpdatePlayer(string gameId, LobbyPlayerState player)
    {
        var entity = db.GamePlayers.Find(gameId.Trim(), player.PlayerId);
        if (entity is null) return;

        entity.DisplayName = player.DisplayName;
        entity.SelectedTerritoryId = player.SelectedTerritoryId;
        db.SaveChanges();
    }

    // ── Mapping helpers ─────────────────────────────────────────────────────

    private static GameEntity ToEntity(LobbyGameState g) => new()
    {
        Id = g.Id,
        Name = g.Name,
        MapArea = g.MapArea,
        MaxHumanPlayers = g.MaxHumanPlayers,
        NpcFactions = g.NpcFactions,
        TerritoryCount = g.TerritoryCount,
        WinningControlPercentage = g.WinningControlPercentage,
        IsStarted = g.IsStarted,
        IsEnded = g.IsEnded,
        WinnerFactionId = g.WinnerFactionId,
        WinnerFactionName = g.WinnerFactionName,
        CreatedAt = g.CreatedAt,
        StartedAt = g.StartedAt,
        EndedAt = g.EndedAt,
        Players = g.Players.Select(p => new GamePlayerEntity
        {
            GameId = g.Id,
            PlayerId = p.PlayerId,
            FactionId = p.FactionId,
            DisplayName = p.DisplayName,
            SelectedTerritoryId = p.SelectedTerritoryId
        }).ToList()
    };

    private static LobbyGameState ToLobbyGameState(GameEntity e)
    {
        var state = new LobbyGameState
        {
            Id = e.Id,
            Name = e.Name,
            MapArea = e.MapArea,
            MaxHumanPlayers = e.MaxHumanPlayers,
            NpcFactions = e.NpcFactions,
            TerritoryCount = e.TerritoryCount,
            WinningControlPercentage = e.WinningControlPercentage,
            IsStarted = e.IsStarted,
            IsEnded = e.IsEnded,
            WinnerFactionId = e.WinnerFactionId,
            WinnerFactionName = e.WinnerFactionName,
            CreatedAt = e.CreatedAt,
            StartedAt = e.StartedAt,
            EndedAt = e.EndedAt
        };
        foreach (var p in e.Players)
        {
            state.Players.Add(new LobbyPlayerState(p.PlayerId, p.FactionId, p.DisplayName)
            {
                SelectedTerritoryId = p.SelectedTerritoryId
            });
        }
        return state;
    }

    private static AvailableGameDto ToAvailableGame(GameEntity e)
    {
        var status = e.IsEnded ? "Ended" : e.IsStarted ? "Started" : "Open";
        return new AvailableGameDto(
            Id: e.Id,
            Name: e.Name,
            Status: status,
            MapArea: e.MapArea,
            HumanPlayers: e.Players.Count,
            MaxHumanPlayers: e.MaxHumanPlayers,
            NpcFactions: e.NpcFactions,
            TerritoryCount: e.TerritoryCount,
            WinningControlPercentage: e.WinningControlPercentage,
            WinnerFactionName: e.WinnerFactionName,
            CreatedAt: e.CreatedAt,
            StartedAt: e.StartedAt,
            EndedAt: e.EndedAt);
    }

    private static MatchSetupOptions ToMatchSetupOptions(GameEntity e)
    {
        var status = e.IsEnded ? "Ended" : e.IsStarted ? "Started" : "Open";
        return new MatchSetupOptions(
            GameId: e.Id,
            MapArea: e.MapArea,
            HumanPlayers: e.Players.Count,
            MaxHumanPlayers: e.MaxHumanPlayers,
            NpcFactions: e.NpcFactions,
            TerritoryCount: e.TerritoryCount,
            Status: status,
            IsStarted: e.IsStarted,
            IsEnded: e.IsEnded,
            WinningControlPercentage: e.WinningControlPercentage,
            WinnerFactionId: e.WinnerFactionId,
            WinnerFactionName: e.WinnerFactionName,
            HumanStartTerritoriesByFactionId: e.Players
                .Where(p => p.SelectedTerritoryId is not null)
                .ToDictionary(p => p.FactionId, p => p.SelectedTerritoryId!, StringComparer.OrdinalIgnoreCase),
            HumanPlayerNamesByFactionId: e.Players
                .ToDictionary(p => p.FactionId, p => p.DisplayName, StringComparer.OrdinalIgnoreCase));
    }
}
