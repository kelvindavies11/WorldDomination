namespace Game.Application;

/// <summary>
/// Computes NPC moves for a single game tick.
/// Each NPC faction that owns at least one territory may move into one
/// adjacent neutral or enemy territory per tick, subject to its <see cref="NpcNature"/>:
/// <list type="bullet">
///   <item><description><b>Active</b> — acts every tick; aggressively attacks adjacent enemies (65% preference over neutral).</description></item>
///   <item><description><b>Conservative</b> — acts every other tick; attacks enemies only when the attacker has more army than the defender.</description></item>
///   <item><description><b>Passive</b> — acts every third tick; never attacks enemies; only expands into neutral territories.</description></item>
/// </list>
/// </summary>
public static class NpcTurnService
{
    /// <summary>
    /// Returns one <see cref="SendArmyCommand"/> per NPC faction that has a
    /// legal neutral-capture move available this tick.
    /// </summary>
    /// <param name="snapshot">Current match state.</param>
    /// <param name="factionTickCounts">
    /// Maps faction ID to the 1-based tick count for that faction.
    /// Factions not present default to tick 1 (acts on every nature).
    /// </param>
    /// <param name="random">Optional RNG; defaults to <see cref="Random.Shared"/>.</param>
    public static IReadOnlyList<SendArmyCommand> PlanMoves(
        MatchSnapshot snapshot,
        IReadOnlyDictionary<string, int>? factionTickCounts = null,
        Random? random = null)
    {
        random ??= Random.Shared;

        if (!snapshot.Game.IsStarted || snapshot.Game.IsEnded)
        {
            return [];
        }

        var moves = new List<SendArmyCommand>();

        var npcFactions = snapshot.Factions
            .Where(f => f.Kind == FactionKind.Npc)
            .ToList();

        // Pre-build adjacency: territory id → set of connected territory ids
        var adjacent = BuildAdjacency(snapshot.Routes);

        foreach (var faction in npcFactions)
        {
            var tickCount = factionTickCounts?.GetValueOrDefault(faction.Id, 1) ?? 1;
            if (!ShouldActThisTick(faction.Nature, tickCount))
            {
                continue;
            }

            var move = PlanMoveForFaction(snapshot, faction.Id, faction.Nature, adjacent, random);
            if (move is not null)
            {
                moves.Add(move);
            }
        }

        return moves;
    }

    /// <summary>Returns true when the given nature allows acting on this tick number.</summary>
    public static bool ShouldActThisTick(NpcNature? nature, int tickCount) =>
        nature switch
        {
            NpcNature.Active => true,
            NpcNature.Conservative => tickCount % 2 == 1,  // ticks 1, 3, 5 …
            NpcNature.Passive => tickCount % 3 == 1,       // ticks 1, 4, 7 …
            _ => true // null treated as Active
        };

    private static SendArmyCommand? PlanMoveForFaction(
        MatchSnapshot snapshot,
        string factionId,
        NpcNature? nature,
        IReadOnlyDictionary<string, List<string>> adjacent,
        Random random)
    {
        // Find territories owned by this NPC that have an army
        var ownedTerritoryIds = snapshot.Territories
            .Where(t => t.OwnerFactionId == factionId)
            .Select(t => t.Id)
            .ToHashSet(StringComparer.Ordinal);

        if (ownedTerritoryIds.Count == 0)
        {
            return null;
        }

        // Build separate candidate lists: neutral and enemy
        var neutralCandidates = new List<(string SourceId, string TargetId, int ArmyStrength)>();
        var enemyCandidates = new List<(string SourceId, string TargetId, int ArmyStrength)>();

        foreach (var sourceId in ownedTerritoryIds)
        {
            var armyStrength = snapshot.Armies
                .Where(a => a.FactionId == factionId && a.TerritoryId == sourceId)
                .Sum(a => a.Strength);

            if (armyStrength <= 0)
            {
                continue;
            }

            if (!adjacent.TryGetValue(sourceId, out var neighbours))
            {
                continue;
            }

            foreach (var neighbourId in neighbours)
            {
                var neighbour = snapshot.Territories.FirstOrDefault(t => t.Id == neighbourId);
                if (neighbour is null)
                {
                    continue;
                }

                if (neighbour.OwnerFactionId is null)
                {
                    neutralCandidates.Add((sourceId, neighbourId, armyStrength));
                }
                else if (neighbour.OwnerFactionId != factionId)
                {
                    enemyCandidates.Add((sourceId, neighbourId, armyStrength));
                }
            }
        }

        // Select the candidate pool based on NPC nature
        List<(string SourceId, string TargetId, int ArmyStrength)> candidates;
        switch (nature)
        {
            case NpcNature.Passive:
                // Passive: only expands into neutral territory; never attacks enemies
                candidates = neutralCandidates;
                break;

            case NpcNature.Conservative:
                // Conservative: prefers neutral expansion; attacks an enemy only when the
                // attacker has strictly more army than the defending garrison
                var viableEnemyCandidates = enemyCandidates
                    .Where(c =>
                    {
                        var defenderStrength = snapshot.Armies
                            .Where(a => a.TerritoryId == c.TargetId)
                            .Sum(a => a.Strength);
                        return c.ArmyStrength > defenderStrength;
                    })
                    .ToList();

                // Prefer neutral; fall back to viable enemies if no neutral targets remain
                candidates = neutralCandidates.Count > 0 ? neutralCandidates : viableEnemyCandidates;
                break;

            default:
                // Active (or null = Active): aggressively prefers enemies when available
                // 65% chance to attack a neighbouring enemy; falls back to neutral
                if (enemyCandidates.Count > 0 && (neutralCandidates.Count == 0 || random.NextDouble() < 0.65))
                {
                    candidates = enemyCandidates;
                }
                else
                {
                    candidates = neutralCandidates.Count > 0 ? neutralCandidates : enemyCandidates;
                }
                break;
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        // Pick a random candidate so NPCs don't all pile into the same direction
        var (chosenSource, chosenTarget, chosenStrength) = candidates[random.Next(candidates.Count)];

        // Send a random amount, keeping at least 1 troop at the source
        var maxSend = Math.Max(1, chosenStrength - 1);
        var sendStrength = random.Next(1, maxSend + 1);

        return new SendArmyCommand(
            PlayerFactionId: factionId,
            SourceTerritoryId: chosenSource,
            TargetTerritoryId: chosenTarget,
            Strength: sendStrength,
            GameId: snapshot.GameId);
    }

    private static Dictionary<string, List<string>> BuildAdjacency(IReadOnlyList<MatchRouteDto> routes)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var route in routes)
        {
            if (!route.IsAllowed)
            {
                continue;
            }

            if (!map.TryGetValue(route.SourceTerritoryId, out var srcList))
            {
                map[route.SourceTerritoryId] = srcList = [];
            }
            srcList.Add(route.DestinationTerritoryId);

            if (!map.TryGetValue(route.DestinationTerritoryId, out var dstList))
            {
                map[route.DestinationTerritoryId] = dstList = [];
            }
            dstList.Add(route.SourceTerritoryId);
        }

        return map;
    }
}
