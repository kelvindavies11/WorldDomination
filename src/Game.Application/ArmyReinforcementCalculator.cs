using Game.Domain;

namespace Game.Application;

/// <summary>
/// Computes reinforcement grants for every faction that owns territory.
/// Each owned territory generates troops each tick based on its Economy stat:
///   <c>max(1, Economy / 10)</c> troops per tick.
/// Territories with no army (freshly captured and still in transit) are skipped —
/// the reinforcements accumulate once an army is present.
/// </summary>
public static class ArmyReinforcementCalculator
{
    /// <summary>
    /// Returns one <see cref="ReinforcementGrant"/> per owned territory that has
    /// an existing army.  Returns an empty list when the game is not started.
    /// </summary>
    public static IReadOnlyList<ReinforcementGrant> Calculate(MatchSnapshot snapshot)
    {
        if (!snapshot.Game.IsStarted || snapshot.Game.IsEnded)
        {
            return [];
        }

        var armyTerritories = snapshot.Armies
            .Select(a => (a.FactionId, a.TerritoryId))
            .ToHashSet();

        var grants = new List<ReinforcementGrant>();

        foreach (var territory in snapshot.Territories)
        {
            if (territory.OwnerFactionId is null)
            {
                continue;
            }

            if (!armyTerritories.Contains((territory.OwnerFactionId, territory.Id)))
            {
                continue;
            }

            var income = Math.Max(1, territory.Stats.Economy / 10);
            grants.Add(new ReinforcementGrant(territory.OwnerFactionId, territory.Id, income));
        }

        return grants;
    }

    /// <summary>
    /// Applies a set of reinforcement grants to a snapshot, adding strength to the
    /// matching army in each territory.
    /// </summary>
    public static MatchSnapshot Apply(MatchSnapshot snapshot, IReadOnlyList<ReinforcementGrant> grants)
    {
        if (grants.Count == 0)
        {
            return snapshot;
        }

        // Build a lookup of added strength keyed by (factionId, territoryId)
        var additions = new Dictionary<(string FactionId, string TerritoryId), int>(grants.Count);
        foreach (var grant in grants)
        {
            var key = (grant.FactionId, grant.TerritoryId);
            additions[key] = additions.GetValueOrDefault(key) + grant.Amount;
        }

        var updatedArmies = snapshot.Armies
            .Select(army =>
            {
                var key = (army.FactionId, army.TerritoryId);
                return additions.TryGetValue(key, out var bonus)
                    ? army with { Strength = army.Strength + bonus }
                    : army;
            })
            .ToList();

        var currentResources = (snapshot.Resources ?? [])
            .ToDictionary(resource => resource.FactionId, resource => resource.Revenue, StringComparer.Ordinal);
        var currentIncome = snapshot.Territories
            .Where(territory => !string.IsNullOrWhiteSpace(territory.OwnerFactionId))
            .GroupBy(territory => territory.OwnerFactionId!, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(territory => territory.Stats.Economy),
                StringComparer.Ordinal);
        var updatedResources = snapshot.Factions
            .Select(faction => new MatchFactionResourceDto(
                faction.Id,
                currentResources.GetValueOrDefault(faction.Id) + currentIncome.GetValueOrDefault(faction.Id)))
            .ToList();

        var leaderboard = MapControlCalculator.CalculateLeaderboard(
            snapshot.Territories.Select(t => new ControlledTerritory(
                t.Id, t.OwnerFactionId, t.AreaSquareKm, t.Stats)).ToArray(),
            snapshot.Factions.Select(f =>
            {
                var current = snapshot.Leaderboard.FirstOrDefault(r => r.FactionId == f.Id);
                return new FactionStanding(f.Id, f.Name, current?.EliminationCount ?? 0);
            }).ToArray(),
            updatedArmies.Select(a => new ControlledArmy(a.FactionId, a.Strength)).ToArray(),
            snapshot.Routes.Select(r => new ConnectedRoute(r.SourceTerritoryId, r.DestinationTerritoryId, r.IsAllowed)).ToArray(),
            updatedResources.ToDictionary(resource => resource.FactionId, resource => resource.Revenue, StringComparer.Ordinal));

        return snapshot with
        {
            SnapshotGeneratedAtUtc = DateTimeOffset.UtcNow,
            Armies = updatedArmies,
            Leaderboard = leaderboard,
            Resources = updatedResources
        };
    }
}

public sealed record ReinforcementGrant(string FactionId, string TerritoryId, int Amount);
