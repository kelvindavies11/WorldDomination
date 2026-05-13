namespace Game.Domain;

public sealed record ControlledTerritory(
    string TerritoryId,
    string? FactionId,
    double AreaSquareKm,
    TerritoryStats? Stats = null);

public sealed record ControlledArmy(
    string FactionId,
    int Strength);

public sealed record ConnectedRoute(
    string SourceTerritoryId,
    string DestinationTerritoryId,
    bool IsAllowed);

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
    bool IsEliminated,
    int TerritoryCount = 0,
    int Revenue = 0,
    int ArmyStrength = 0,
    int ArmyGrowth = 0);

public static class MapControlCalculator
{
    public static IReadOnlyList<LeaderboardRow> CalculateLeaderboard(
        IReadOnlyCollection<ControlledTerritory> territories,
        IReadOnlyCollection<FactionStanding> factions,
        IReadOnlyCollection<ControlledArmy>? armies = null,
        IReadOnlyCollection<ConnectedRoute>? routes = null,
        IReadOnlyDictionary<string, int>? revenueTotals = null)
    {
        armies ??= [];
        routes ??= [];
        var totalArea = territories.Sum(territory => territory.AreaSquareKm);

        var rows = factions
            .Select(faction =>
            {
                var ownedTerritories = territories
                    .Where(territory => territory.FactionId == faction.FactionId)
                    .ToArray();
                var controlledArea = territories
                    .Where(territory => territory.FactionId == faction.FactionId)
                    .Sum(territory => territory.AreaSquareKm);
                var territoryCount = ownedTerritories.Length;
                var economicIncome = ownedTerritories.Sum(territory => territory.Stats?.Economy ?? 0);
                var revenue = revenueTotals?.GetValueOrDefault(faction.FactionId, economicIncome) ?? economicIncome;
                var strategicValue = ownedTerritories.Sum(territory => territory.Stats?.StrategicValue ?? 0);
                var connectedMobilityBonus = CalculateConnectedMobilityBonus(ownedTerritories, routes);
                var armyStrength = armies
                    .Where(army => army.FactionId == faction.FactionId)
                    .Sum(army => army.Strength);

                var percentage = totalArea <= 0
                    ? 0
                    : Math.Round(controlledArea / totalArea * 100, 1, MidpointRounding.AwayFromZero);

                return new LeaderboardRow(
                    Rank: 0,
                    FactionId: faction.FactionId,
                    FactionName: faction.Name,
                    MapControlPercentage: percentage,
                    EliminationCount: faction.EliminationCount,
                        IsEliminated: controlledArea <= 0,
                        TerritoryCount: territoryCount,
                        Revenue: revenue,
                        ArmyStrength: armyStrength,
                        ArmyGrowth: economicIncome + strategicValue + connectedMobilityBonus);
            })
            .OrderByDescending(row => row.MapControlPercentage)
            .ThenByDescending(row => row.EliminationCount)
            .ThenBy(row => row.FactionName, StringComparer.Ordinal)
            .ToList();

        return rows
            .Select((row, index) => row with { Rank = index + 1 })
            .ToList();
    }

    private static int CalculateConnectedMobilityBonus(
        IReadOnlyCollection<ControlledTerritory> ownedTerritories,
        IReadOnlyCollection<ConnectedRoute> routes)
    {
        if (ownedTerritories.Count == 0)
        {
            return 0;
        }

        var ownedTerritoryIds = ownedTerritories
            .Select(territory => territory.TerritoryId)
            .ToHashSet(StringComparer.Ordinal);

        return ownedTerritories
            .Where(territory => territory.Stats is not null)
            .Where(territory => routes.Any(route =>
                route.IsAllowed &&
                ((route.SourceTerritoryId == territory.TerritoryId && ownedTerritoryIds.Contains(route.DestinationTerritoryId)) ||
                 (route.DestinationTerritoryId == territory.TerritoryId && ownedTerritoryIds.Contains(route.SourceTerritoryId)))))
            .Sum(territory => territory.Stats!.Mobility);
    }

    public static string? FindWinner(IReadOnlyCollection<ControlledTerritory> territories, double requiredControlPercentage = 100)
    {
        var totalArea = territories.Sum(territory => territory.AreaSquareKm);
        if (totalArea <= 0)
        {
            return null;
        }

        var thresholdRatio = Math.Clamp(requiredControlPercentage, 0, 100) / 100d;

        return territories
            .Where(territory => !string.IsNullOrWhiteSpace(territory.FactionId))
            .GroupBy(territory => territory.FactionId)
            .Where(group => group.Sum(territory => territory.AreaSquareKm) >= totalArea * thresholdRatio)
            .Select(group => group.Key)
            .SingleOrDefault();
    }
}
