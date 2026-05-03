namespace Game.Domain;

public sealed record ControlledTerritory(
    string TerritoryId,
    string? FactionId,
    double AreaSquareKm);

public sealed record FactionStanding(
    string FactionId,
    string Name,
    int EliminationCount);

public sealed record LeaderboardRow(
    int Rank,
    string FactionId,
    string FactionName,
    double MapControlPercentage,
    int EliminationCount,
    bool IsEliminated);

public static class MapControlCalculator
{
    public static IReadOnlyList<LeaderboardRow> CalculateLeaderboard(
        IReadOnlyCollection<ControlledTerritory> territories,
        IReadOnlyCollection<FactionStanding> factions)
    {
        var totalArea = territories.Sum(territory => territory.AreaSquareKm);

        var rows = factions
            .Select(faction =>
            {
                var controlledArea = territories
                    .Where(territory => territory.FactionId == faction.FactionId)
                    .Sum(territory => territory.AreaSquareKm);

                var percentage = totalArea <= 0
                    ? 0
                    : Math.Round(controlledArea / totalArea * 100, 1, MidpointRounding.AwayFromZero);

                return new LeaderboardRow(
                    Rank: 0,
                    FactionId: faction.FactionId,
                    FactionName: faction.Name,
                    MapControlPercentage: percentage,
                    EliminationCount: faction.EliminationCount,
                    IsEliminated: controlledArea <= 0);
            })
            .OrderByDescending(row => row.MapControlPercentage)
            .ThenByDescending(row => row.EliminationCount)
            .ThenBy(row => row.FactionName, StringComparer.Ordinal)
            .ToList();

        return rows
            .Select((row, index) => row with { Rank = index + 1 })
            .ToList();
    }

    public static string? FindWinner(IReadOnlyCollection<ControlledTerritory> territories)
    {
        var totalArea = territories.Sum(territory => territory.AreaSquareKm);
        if (totalArea <= 0)
        {
            return null;
        }

        return territories
            .Where(territory => !string.IsNullOrWhiteSpace(territory.FactionId))
            .GroupBy(territory => territory.FactionId)
            .Where(group => group.Sum(territory => territory.AreaSquareKm) >= totalArea)
            .Select(group => group.Key)
            .SingleOrDefault();
    }
}
