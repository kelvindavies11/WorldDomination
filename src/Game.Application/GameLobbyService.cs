namespace Game.Application;

public sealed record AvailableGameDto(
    string Id,
    string Name,
    string Status,
    string MapArea,
    int HumanPlayers,
    int MaxHumanPlayers,
    int NpcFactions,
    int TerritoryCount);

public sealed record CreateGameRequest(
    string Name,
    string MapArea,
    int MaxHumanPlayers,
    int NpcFactions,
    int TerritoryCount);

public sealed class GameLobbyService
{
    private readonly List<AvailableGameDto> games = [];

    private int nextGameNumber = 1;

    public IReadOnlyList<AvailableGameDto> ListAvailableGames() =>
        games
            .OrderBy(game => game.Status == "Open" ? 0 : 1)
            .ThenBy(game => game.Name, StringComparer.Ordinal)
            .ToList();

    public AvailableGameDto CreateGame(CreateGameRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.MapArea);

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

        var game = new AvailableGameDto(
            Id: $"game-{nextGameNumber++:000}",
            Name: request.Name.Trim(),
            Status: "Open",
            MapArea: request.MapArea.Trim(),
            HumanPlayers: 1,
            MaxHumanPlayers: request.MaxHumanPlayers,
            NpcFactions: request.NpcFactions,
            TerritoryCount: request.TerritoryCount);

        games.Add(game);
        return game;
    }

    public bool EndGame(string gameId)
    {
        var game = games.FirstOrDefault(item => string.Equals(item.Id, gameId.Trim(), StringComparison.OrdinalIgnoreCase));
        if (game is null)
        {
            return false;
        }

        games.Remove(game);
        return true;
    }
}
