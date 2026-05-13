namespace Game.Application;

public interface IGameRepository
{
    IReadOnlyList<AvailableGameDto> ListAvailableGames();

    LobbyGameState? GetById(string gameId);

    MatchSetupOptions? GetMatchSetup(string gameId);

    /// <summary>Returns the highest game number used so far (e.g. 3 for "game-003"), or 0 if no games exist.</summary>
    int GetMaxGameNumber();

    void Add(LobbyGameState game);

    void Update(LobbyGameState game);

    void AddPlayer(string gameId, LobbyPlayerState player);

    void UpdatePlayer(string gameId, LobbyPlayerState player);
}
