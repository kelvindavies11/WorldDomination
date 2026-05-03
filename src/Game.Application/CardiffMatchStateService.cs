namespace Game.Application;

public sealed class CardiffMatchStateService
{
    private readonly object sync = new();
    private readonly CardiffMatchService matchService;
    private readonly Dictionary<string, MatchSnapshot> snapshots = new(StringComparer.OrdinalIgnoreCase);

    public CardiffMatchStateService(CardiffMatchService matchService)
    {
        this.matchService = matchService;
    }

    public CardiffMatchStateService(MatchSnapshot snapshot)
    {
        matchService = new CardiffMatchService(new GameMapService());
        snapshots[snapshot.GameId] = snapshot;
    }

    public MatchSnapshot GetSnapshot(string gameId = "cardiff-match")
    {
        lock (sync)
        {
            var normalizedGameId = NormalizeGameId(gameId);
            if (!snapshots.TryGetValue(normalizedGameId, out var snapshot))
            {
                snapshot = matchService.CreateCardiffMatch(normalizedGameId);
                snapshots[normalizedGameId] = snapshot;
            }

            return snapshot;
        }
    }

    public MatchSnapshot Update(string gameId, Func<MatchSnapshot, MatchSnapshot> update)
    {
        lock (sync)
        {
            var normalizedGameId = NormalizeGameId(gameId);
            if (!snapshots.TryGetValue(normalizedGameId, out var snapshot))
            {
                snapshot = matchService.CreateCardiffMatch(normalizedGameId);
            }

            snapshot = update(snapshot);
            snapshots[normalizedGameId] = snapshot;
            return snapshot;
        }
    }

    public MatchSnapshot Update(Func<MatchSnapshot, MatchSnapshot> update) =>
        Update("cardiff-match", update);

    private static string NormalizeGameId(string gameId) =>
        string.Equals(gameId, "cardiff", StringComparison.OrdinalIgnoreCase)
            ? "cardiff-match"
            : gameId.Trim();
}
