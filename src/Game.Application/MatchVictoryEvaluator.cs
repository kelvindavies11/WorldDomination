namespace Game.Application;

public sealed record MatchWinner(
    string FactionId,
    string FactionName,
    double ControlPercentage);

public static class MatchVictoryEvaluator
{
    public static MatchWinner? TryGetWinner(MatchSnapshot snapshot)
    {
        if (snapshot.Game.IsEnded)
        {
            return snapshot.Game.WinnerFactionId is null || snapshot.Game.WinnerFactionName is null
                ? null
                : new MatchWinner(
                    snapshot.Game.WinnerFactionId,
                    snapshot.Game.WinnerFactionName,
                    snapshot.Leaderboard.FirstOrDefault(row => row.FactionId == snapshot.Game.WinnerFactionId)?.MapControlPercentage ?? 0);
        }

        // If all human players have been eliminated, end the game immediately
        var humanFactionIds = snapshot.Factions
            .Where(f => f.Kind == FactionKind.Human)
            .Select(f => f.Id)
            .ToHashSet(StringComparer.Ordinal);

        if (humanFactionIds.Count > 0)
        {
            var anyHumanHasTerritories = snapshot.Territories
                .Any(t => t.OwnerFactionId is not null && humanFactionIds.Contains(t.OwnerFactionId));

            if (!anyHumanHasTerritories)
            {
                var topNpc = snapshot.Leaderboard
                    .Where(row => !humanFactionIds.Contains(row.FactionId))
                    .OrderByDescending(row => row.MapControlPercentage)
                    .FirstOrDefault();

                if (topNpc is not null)
                {
                    return new MatchWinner(topNpc.FactionId, topNpc.FactionName, topNpc.MapControlPercentage);
                }
            }
        }

        var winningRow = snapshot.Leaderboard
            .OrderByDescending(row => row.MapControlPercentage)
            .FirstOrDefault();

        if (winningRow is null || winningRow.MapControlPercentage < snapshot.Game.WinningControlPercentage)
        {
            return null;
        }

        return new MatchWinner(winningRow.FactionId, winningRow.FactionName, winningRow.MapControlPercentage);
    }

    public static MatchSnapshot ApplyVictory(MatchSnapshot snapshot, MatchWinner winner) =>
        snapshot with
        {
            SnapshotGeneratedAtUtc = DateTimeOffset.UtcNow,
            Game = snapshot.Game with
            {
                Status = "Ended",
                IsStarted = false,
                IsEnded = true,
                WinnerFactionId = winner.FactionId,
                WinnerFactionName = winner.FactionName
            }
        };

    /// <summary>Marks the game as ended due to inactivity stalemate with no winner.</summary>
    public static MatchSnapshot ApplyStalemateEnd(MatchSnapshot snapshot) =>
        snapshot with
        {
            SnapshotGeneratedAtUtc = DateTimeOffset.UtcNow,
            Game = snapshot.Game with
            {
                Status = "Ended",
                IsStarted = false,
                IsEnded = true,
                WinnerFactionId = null,
                WinnerFactionName = null
            }
        };
}