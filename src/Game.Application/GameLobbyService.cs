namespace Game.Application;

public sealed record MatchSetupOptions(
    string GameId,
    string MapArea,
    int HumanPlayers,
    int MaxHumanPlayers,
    int NpcFactions,
    int TerritoryCount,
    string Status,
    bool IsStarted,
    IReadOnlyDictionary<string, string> HumanStartTerritoriesByFactionId,
    IReadOnlyDictionary<string, string> HumanPlayerNamesByFactionId,
    bool IsEnded = false,
    double WinningControlPercentage = 100,
    string? WinnerFactionId = null,
    string? WinnerFactionName = null);

public sealed record AvailableGameDto(
    string Id,
    string Name,
    string Status,
    string MapArea,
    int HumanPlayers,
    int MaxHumanPlayers,
    int NpcFactions,
    int TerritoryCount,
    double WinningControlPercentage = 100,
    string? WinnerFactionName = null,
    DateTimeOffset? CreatedAt = null,
    DateTimeOffset? StartedAt = null,
    DateTimeOffset? EndedAt = null);

public sealed record CreateGameRequest(
    string Name,
    string MapArea,
    int MaxHumanPlayers,
    int NpcFactions,
    int TerritoryCount,
    double WinningControlPercentage = 100);

public sealed class LobbyPlayerState
{
    public LobbyPlayerState(string playerId, string factionId, string displayName)
    {
        PlayerId = playerId;
        FactionId = factionId;
        DisplayName = displayName;
    }

    public string PlayerId { get; }

    public string FactionId { get; }

    public string DisplayName { get; set; }

    public string? SelectedTerritoryId { get; set; }
}

public sealed class LobbyGameState
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string MapArea { get; init; }

    public int MaxHumanPlayers { get; init; }

    public int NpcFactions { get; init; }

    public int TerritoryCount { get; init; }

    public double WinningControlPercentage { get; init; } = 100;

    public bool IsStarted { get; set; }

    public bool IsEnded { get; set; }

    public string? WinnerFactionId { get; set; }

    public string? WinnerFactionName { get; set; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? EndedAt { get; set; }

    public List<LobbyPlayerState> Players { get; } = [];
}

public sealed class GameLobbyService
{
    private readonly IGameRepository repo;

    public GameLobbyService(IGameRepository repository)
    {
        repo = repository;
    }

    public IReadOnlyList<AvailableGameDto> ListAvailableGames() => repo.ListAvailableGames();

    public AvailableGameDto CreateGame(CreateGameRequest request, string playerId, string? playerDisplayName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.MapArea);
        ArgumentException.ThrowIfNullOrWhiteSpace(playerId);

        if (request.MaxHumanPlayers < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(request.MaxHumanPlayers), "A game needs at least one human player slot.");
        }

        if (request.NpcFactions < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request.NpcFactions), "NPC faction count cannot be negative.");
        }

        if (request.TerritoryCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(request.TerritoryCount), "A game needs at least one territory.");
        }

        if (request.WinningControlPercentage <= 0 || request.WinningControlPercentage > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(request.WinningControlPercentage), "Winning control percentage must be greater than 0 and less than or equal to 100.");
        }

        if (request.MaxHumanPlayers + request.NpcFactions > request.TerritoryCount)
        {
            throw new ArgumentException("Faction count cannot exceed the available territory count.", nameof(request));
        }

        var nextNumber = repo.GetMaxGameNumber() + 1;
        var game = new LobbyGameState
        {
            Id = $"game-{nextNumber:000}",
            Name = request.Name.Trim(),
            MapArea = request.MapArea.Trim(),
            MaxHumanPlayers = request.MaxHumanPlayers,
            NpcFactions = request.NpcFactions,
            TerritoryCount = request.TerritoryCount,
            WinningControlPercentage = request.WinningControlPercentage
        };

        game.Players.Add(new LobbyPlayerState(playerId.Trim(), "human-1", ResolveDisplayName(playerDisplayName, 1)));
        repo.Add(game);
        return ToAvailableGame(game);
    }

    public JoinGameResponse JoinGame(string gameId, string playerId, string? playerDisplayName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playerId);

        var game = FindGame(gameId) ?? throw new InvalidOperationException("Game was not found.");
        var normalizedPlayerId = playerId.Trim();
        var existing = game.Players.FirstOrDefault(player => string.Equals(player.PlayerId, normalizedPlayerId, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            if (!string.IsNullOrWhiteSpace(playerDisplayName))
            {
                existing.DisplayName = playerDisplayName.Trim();
                repo.UpdatePlayer(game.Id, existing);
            }
            return new JoinGameResponse(game.Id, existing.FactionId, existing.DisplayName, StatusText(game), game.Players.Count, game.MaxHumanPlayers);
        }

        if (game.IsStarted)
        {
            throw new InvalidOperationException("Game has already started.");
        }

        if (game.Players.Count >= game.MaxHumanPlayers)
        {
            throw new InvalidOperationException("Game is full.");
        }

        var factionId = $"human-{game.Players.Count + 1}";
        var newPlayer = new LobbyPlayerState(normalizedPlayerId, factionId, ResolveDisplayName(playerDisplayName, game.Players.Count + 1));
        repo.AddPlayer(game.Id, newPlayer);
        return new JoinGameResponse(game.Id, factionId, newPlayer.DisplayName, StatusText(game), game.Players.Count + 1, game.MaxHumanPlayers);
    }

    public string SelectStartPosition(string gameId, string playerId, string territoryId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(territoryId);

        var game = FindGame(gameId) ?? throw new InvalidOperationException("Game was not found.");
        if (game.IsStarted)
        {
            throw new InvalidOperationException("Game has already started.");
        }

        var player = game.Players.FirstOrDefault(item => string.Equals(item.PlayerId, playerId.Trim(), StringComparison.OrdinalIgnoreCase));
        if (player is null)
        {
            throw new InvalidOperationException("Player has not joined this game.");
        }

        if (!string.IsNullOrWhiteSpace(player.SelectedTerritoryId))
        {
            throw new InvalidOperationException("Player has already selected a starting territory.");
        }

        if (game.Players.Any(item => string.Equals(item.SelectedTerritoryId, territoryId.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Territory has already been claimed as a starting position.");
        }

        player.SelectedTerritoryId = territoryId.Trim();
        repo.UpdatePlayer(game.Id, player);
        return player.FactionId;
    }

    public AvailableGameDto StartGame(string gameId, string playerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playerId);

        var game = FindGame(gameId) ?? throw new InvalidOperationException("Game was not found.");
        if (game.IsStarted)
        {
            return ToAvailableGame(game);
        }

        if (!game.Players.Any(item => string.Equals(item.PlayerId, playerId.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Player has not joined this game.");
        }

        if (game.Players.Any(item => string.IsNullOrWhiteSpace(item.SelectedTerritoryId)))
        {
            throw new InvalidOperationException("All joined players must select a starting territory before the game can start.");
        }

        game.IsStarted = true;
        game.StartedAt = DateTimeOffset.UtcNow;
        repo.Update(game);
        return ToAvailableGame(game);
    }

    public MatchSetupOptions? GetMatchSetup(string gameId) => repo.GetMatchSetup(gameId);

    public bool EndGame(string gameId, string? winnerFactionId = null, string? winnerFactionName = null)
    {
        var game = FindGame(gameId);
        if (game is null)
        {
            return false;
        }

        game.IsEnded = true;
        game.EndedAt = DateTimeOffset.UtcNow;
        game.WinnerFactionId = winnerFactionId;
        game.WinnerFactionName = winnerFactionName;
        repo.Update(game);
        return true;
    }

    private LobbyGameState? FindGame(string gameId) => repo.GetById(gameId.Trim());

    private static string ResolveDisplayName(string? requested, int slotNumber) =>
        string.IsNullOrWhiteSpace(requested) ? $"Player {slotNumber}" : requested.Trim();

    private static string StatusText(LobbyGameState game) =>
        game.IsEnded ? "Ended" : game.IsStarted ? "Started" : "Open";

    private static AvailableGameDto ToAvailableGame(LobbyGameState game) =>
        new(
            Id: game.Id,
            Name: game.Name,
            Status: StatusText(game),
            MapArea: game.MapArea,
            HumanPlayers: game.Players.Count,
            MaxHumanPlayers: game.MaxHumanPlayers,
            NpcFactions: game.NpcFactions,
            TerritoryCount: game.TerritoryCount,
            WinningControlPercentage: game.WinningControlPercentage,
            WinnerFactionName: game.WinnerFactionName,
            CreatedAt: game.CreatedAt,
            StartedAt: game.StartedAt,
            EndedAt: game.EndedAt);
}
