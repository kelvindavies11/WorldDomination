using Game.Domain;

namespace Game.Application;

public sealed class PlayerTerritoryCommandService
{
    private readonly CardiffMatchStateService stateService;

    public PlayerTerritoryCommandService(CardiffMatchStateService stateService)
    {
        this.stateService = stateService;
    }

    public SendArmyResult SendArmyToNeutralTerritory(SendArmyCommand command)
    {
        return ExecuteTakeover(command);
    }

    public SendArmyResult SendArmy(SendArmyCommand command)
    {
        var snapshot = stateService.GetSnapshot(command.GameId);
        if (!snapshot.Game.IsStarted || snapshot.Game.IsEnded)
            return Rejected("Game is not active.");

        var target = snapshot.Territories.SingleOrDefault(t => t.Id == command.TargetTerritoryId);
        if (target is null)
            return Rejected("Match territory was not found.");

        // Friendly target → reinforce, otherwise → takeover
        return target.OwnerFactionId == command.PlayerFactionId
            ? ExecuteReinforcement(command)
            : ExecuteTakeover(command);
    }

    public SendArmyResult ExecuteReinforcement(SendArmyCommand command)
    {
        var snapshot = stateService.GetSnapshot(command.GameId);
        if (!snapshot.Game.IsStarted || snapshot.Game.IsEnded)
            return Rejected("Game is not active.");

        var source = snapshot.Territories.SingleOrDefault(t => t.Id == command.SourceTerritoryId);
        var target = snapshot.Territories.SingleOrDefault(t => t.Id == command.TargetTerritoryId);
        if (source is null || target is null)
            return Rejected("Match territory was not found.");

        var route = FindRoute(snapshot, source.Id, target.Id);
        var sourceArmyStrength = snapshot.Armies
            .Where(a => a.FactionId == command.PlayerFactionId && a.TerritoryId == source.Id)
            .Sum(a => a.Strength);
        var validation = TerritoryExpansion.ValidateReinforcement(new ReinforcementRequest(
            ActingFactionId: command.PlayerFactionId,
            SourceTerritoryId: source.Id,
            SourceOwnerFactionId: source.OwnerFactionId,
            TargetTerritoryId: target.Id,
            TargetOwnerFactionId: target.OwnerFactionId,
            AvailableArmyStrength: sourceArmyStrength,
            RequestedStrength: command.Strength,
            HasAllowedRoute: route?.IsAllowed == true));

        if (!validation.IsValid)
            return Rejected(validation.Error ?? "Reinforcement command is invalid.");

        var updated = stateService.Update(command.GameId, current => ApplyReinforcementSnapshot(current, command));
        stateService.TrackTerritoryMovement(command.GameId);
        return new SendArmyResult(true, null, route!.EtaSeconds, updated, null);
    }

    private static MatchSnapshot ApplyReinforcementSnapshot(MatchSnapshot snapshot, SendArmyCommand command)
    {
        var sourceArmy = snapshot.Armies.First(a =>
            a.FactionId == command.PlayerFactionId && a.TerritoryId == command.SourceTerritoryId);
        var targetArmy = snapshot.Armies.FirstOrDefault(a =>
            a.FactionId == command.PlayerFactionId && a.TerritoryId == command.TargetTerritoryId);
        var result = TerritoryExpansion.ApplyReinforcement(new ReinforcementArmyState(
            SourceArmyStrength: sourceArmy.Strength,
            RequestedStrength: command.Strength,
            TargetArmyStrength: targetArmy?.Strength ?? 0));

        var armies = snapshot.Armies
            .Where(a => a.Id != sourceArmy.Id && (targetArmy is null || a.Id != targetArmy.Id))
            .Append(sourceArmy with { Strength = result.SourceArmyStrength })
            .Append(targetArmy is not null
                ? targetArmy with { Strength = result.TargetArmyStrength }
                : new MatchArmyDto(
                    Id: $"army-{command.PlayerFactionId}-{command.TargetTerritoryId}",
                    FactionId: command.PlayerFactionId,
                    TerritoryId: command.TargetTerritoryId,
                    Strength: result.TargetArmyStrength))
            .Where(a => a.Strength > 0)
            .ToList();

        return snapshot with
        {
            SnapshotGeneratedAtUtc = DateTimeOffset.UtcNow,
            Armies = armies
        };
    }

    public SendArmyResult ExecuteTakeover(SendArmyCommand command)
    {
        var snapshot = stateService.GetSnapshot(command.GameId);
        if (!snapshot.Game.IsStarted || snapshot.Game.IsEnded)
        {
            return Rejected("Game is not active.");
        }

        var source = snapshot.Territories.SingleOrDefault(territory => territory.Id == command.SourceTerritoryId);
        var target = snapshot.Territories.SingleOrDefault(territory => territory.Id == command.TargetTerritoryId);
        if (source is null || target is null)
        {
            return Rejected("Match territory was not found.");
        }

        var route = FindRoute(snapshot, source.Id, target.Id);
        var sourceArmyStrength = snapshot.Armies
            .Where(army => army.FactionId == command.PlayerFactionId && army.TerritoryId == source.Id)
            .Sum(army => army.Strength);
        var validation = TerritoryExpansion.ValidateTakeover(new TerritoryTakeoverRequest(
            ActingFactionId: command.PlayerFactionId,
            SourceTerritoryId: source.Id,
            SourceOwnerFactionId: source.OwnerFactionId,
            TargetTerritoryId: target.Id,
            TargetOwnerFactionId: target.OwnerFactionId,
            AvailableArmyStrength: sourceArmyStrength,
            RequestedStrength: command.Strength,
            HasAllowedRoute: route?.IsAllowed == true));

        if (!validation.IsValid)
        {
            return Rejected(validation.Error ?? "Movement command is invalid.");
        }

        var priorOwner = snapshot.Territories.First(territory => territory.Id == command.TargetTerritoryId).OwnerFactionId;
        var updated = stateService.Update(command.GameId, current => ApplyTakeover(current, command));
        var newOwner = updated.Territories.First(territory => territory.Id == command.TargetTerritoryId).OwnerFactionId;
        if (priorOwner != newOwner)
        {
            stateService.TrackTerritoryMovement(command.GameId);
        }

        // Detect elimination: prior owner had territories, now has none
        string? eliminatedFactionName = null;
        if (priorOwner is not null && priorOwner != command.PlayerFactionId)
        {
            var hadTerritories = snapshot.Territories.Any(t => t.OwnerFactionId == priorOwner);
            var stillHasTerritories = updated.Territories.Any(t => t.OwnerFactionId == priorOwner);
            if (hadTerritories && !stillHasTerritories)
            {
                eliminatedFactionName = snapshot.Factions.FirstOrDefault(f => f.Id == priorOwner)?.Name ?? priorOwner;
            }
        }

        return new SendArmyResult(true, null, route!.EtaSeconds, updated, eliminatedFactionName);
    }

    /// <summary>
    /// Same validation + capture logic used by NPC ticks.
    /// Does NOT reset the human-inactivity stalemate timer.
    /// </summary>
    public SendArmyResult ExecuteNeutralCapture(SendArmyCommand command)
    {
        var snapshot = stateService.GetSnapshot(command.GameId);
        if (!snapshot.Game.IsStarted || snapshot.Game.IsEnded)
            return Rejected("Game is not active.");

        var source = snapshot.Territories.SingleOrDefault(t => t.Id == command.SourceTerritoryId);
        var target = snapshot.Territories.SingleOrDefault(t => t.Id == command.TargetTerritoryId);
        if (source is null || target is null)
            return Rejected("Match territory was not found.");

        var route = FindRoute(snapshot, source.Id, target.Id);
        var sourceArmyStrength = snapshot.Armies
            .Where(a => a.FactionId == command.PlayerFactionId && a.TerritoryId == source.Id)
            .Sum(a => a.Strength);
        var validation = TerritoryExpansion.ValidateTakeover(new TerritoryTakeoverRequest(
            ActingFactionId: command.PlayerFactionId,
            SourceTerritoryId: source.Id,
            SourceOwnerFactionId: source.OwnerFactionId,
            TargetTerritoryId: target.Id,
            TargetOwnerFactionId: target.OwnerFactionId,
            AvailableArmyStrength: sourceArmyStrength,
            RequestedStrength: command.Strength,
            HasAllowedRoute: route?.IsAllowed == true));

        if (!validation.IsValid)
            return Rejected(validation.Error ?? "Movement command is invalid.");

        var priorOwnerNpc = snapshot.Territories.First(t => t.Id == command.TargetTerritoryId).OwnerFactionId;
        var updated = stateService.Update(command.GameId, current => ApplyTakeover(current, command));
        // Intentionally no TrackTerritoryMovement — NPC moves don't reset human inactivity timer

        // Detect elimination caused by NPC move
        string? eliminatedFactionName = null;
        if (priorOwnerNpc is not null && priorOwnerNpc != command.PlayerFactionId)
        {
            var hadTerritories = snapshot.Territories.Any(t => t.OwnerFactionId == priorOwnerNpc);
            var stillHasTerritories = updated.Territories.Any(t => t.OwnerFactionId == priorOwnerNpc);
            if (hadTerritories && !stillHasTerritories)
            {
                eliminatedFactionName = snapshot.Factions.FirstOrDefault(f => f.Id == priorOwnerNpc)?.Name ?? priorOwnerNpc;
            }
        }

        return new SendArmyResult(true, null, route!.EtaSeconds, updated, eliminatedFactionName);
    }

    private static SendArmyResult Rejected(string error) => new(false, error, null, null);

    private static MatchRouteDto? FindRoute(MatchSnapshot snapshot, string sourceId, string targetId) =>
        snapshot.Routes.FirstOrDefault(route =>
            route.SourceTerritoryId == sourceId && route.DestinationTerritoryId == targetId ||
            route.SourceTerritoryId == targetId && route.DestinationTerritoryId == sourceId);

    private static MatchSnapshot ApplyTakeover(MatchSnapshot snapshot, SendArmyCommand command)
    {
        var sourceArmy = snapshot.Armies.First(army =>
            army.FactionId == command.PlayerFactionId &&
            army.TerritoryId == command.SourceTerritoryId);
        var target = snapshot.Territories.First(territory => territory.Id == command.TargetTerritoryId);
        var takeover = TerritoryExpansion.ResolveTakeover(new TerritoryTakeoverArmyState(
            ActingFactionId: command.PlayerFactionId,
            TargetOwnerFactionId: target.OwnerFactionId,
            SourceArmyStrength: sourceArmy.Strength,
            RequestedStrength: command.Strength,
            DefenderArmyStrength: snapshot.Armies
                .Where(army => army.TerritoryId == command.TargetTerritoryId && army.FactionId == target.OwnerFactionId)
                .Sum(army => army.Strength),
            TerritoryDefense: target.Stats.Defense));
        var territories = snapshot.Territories
            .Select(territory => territory.Id == command.TargetTerritoryId
                ? territory with { OwnerFactionId = takeover.TargetOwnerFactionId }
                : territory)
            .ToList();
        var armies = snapshot.Armies
            .Where(army => army.Id != sourceArmy.Id && army.TerritoryId != command.TargetTerritoryId)
            .Append(sourceArmy with { Strength = takeover.SourceArmyStrength })
            .Append(new MatchArmyDto(
                Id: $"army-{takeover.TargetOwnerFactionId}-{command.TargetTerritoryId}",
                FactionId: takeover.TargetOwnerFactionId ?? command.PlayerFactionId,
                TerritoryId: command.TargetTerritoryId,
                Strength: takeover.TargetArmyStrength))
            .Where(army => army.Strength > 0)
            .ToList();
        var leaderboard = MapControlCalculator.CalculateLeaderboard(
            territories.Select(territory => new ControlledTerritory(
                territory.Id,
                territory.OwnerFactionId,
                territory.AreaSquareKm,
                territory.Stats)).ToArray(),
            snapshot.Factions.Select(faction =>
            {
                var current = snapshot.Leaderboard.FirstOrDefault(row => row.FactionId == faction.Id);
                var elimCount = current?.EliminationCount ?? 0;
                // If this faction just eliminated the prior owner, increment their count
                if (faction.Id == command.PlayerFactionId)
                {
                    var priorOwnerId = snapshot.Territories.First(t => t.Id == command.TargetTerritoryId).OwnerFactionId;
                    if (priorOwnerId is not null && priorOwnerId != command.PlayerFactionId)
                    {
                        var priorOwnerHadTerritories = snapshot.Territories.Any(t => t.OwnerFactionId == priorOwnerId);
                        var priorOwnerStillHas = territories.Any(t => t.OwnerFactionId == priorOwnerId);
                        if (priorOwnerHadTerritories && !priorOwnerStillHas)
                            elimCount++;
                    }
                }
                return new FactionStanding(faction.Id, faction.Name, elimCount);
            }).ToArray(),
            armies.Select(army => new ControlledArmy(army.FactionId, army.Strength)).ToArray(),
            snapshot.Routes.Select(route => new ConnectedRoute(route.SourceTerritoryId, route.DestinationTerritoryId, route.IsAllowed)).ToArray(),
            snapshot.Resources?.ToDictionary(resource => resource.FactionId, resource => resource.Revenue, StringComparer.Ordinal));

        return snapshot with
        {
            SnapshotGeneratedAtUtc = DateTimeOffset.UtcNow,
            Territories = territories,
            Armies = armies,
            Leaderboard = leaderboard,
            Resources = snapshot.Resources
        };
    }
}
