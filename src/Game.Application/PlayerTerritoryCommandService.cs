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
        var snapshot = stateService.GetSnapshot(command.GameId);
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
        var validation = TerritoryExpansion.ValidateNeutralCapture(new NeutralCaptureRequest(
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

        var updated = stateService.Update(command.GameId, current => ApplyCapture(current, command, route!));
        return new SendArmyResult(true, null, route!.EtaSeconds, updated);
    }

    private static SendArmyResult Rejected(string error) => new(false, error, null, null);

    private static MatchRouteDto? FindRoute(MatchSnapshot snapshot, string sourceId, string targetId) =>
        snapshot.Routes.FirstOrDefault(route =>
            route.SourceTerritoryId == sourceId && route.DestinationTerritoryId == targetId ||
            route.SourceTerritoryId == targetId && route.DestinationTerritoryId == sourceId);

    private static MatchSnapshot ApplyCapture(MatchSnapshot snapshot, SendArmyCommand command, MatchRouteDto route)
    {
        var sourceArmy = snapshot.Armies.First(army =>
            army.FactionId == command.PlayerFactionId &&
            army.TerritoryId == command.SourceTerritoryId);
        var capture = TerritoryExpansion.ApplyNeutralCapture(new NeutralCaptureArmyState(
            command.PlayerFactionId,
            sourceArmy.Strength,
            command.Strength));
        var territories = snapshot.Territories
            .Select(territory => territory.Id == command.TargetTerritoryId
                ? territory with { OwnerFactionId = capture.TargetOwnerFactionId }
                : territory)
            .ToList();
        var armies = snapshot.Armies
            .Select(army => army.Id == sourceArmy.Id
                ? army with { Strength = capture.SourceArmyStrength }
                : army)
            .Append(new MatchArmyDto(
                Id: $"army-{command.PlayerFactionId}-{command.TargetTerritoryId}",
                FactionId: command.PlayerFactionId,
                TerritoryId: command.TargetTerritoryId,
                Strength: capture.TargetArmyStrength))
            .Where(army => army.Strength > 0)
            .ToList();
        var leaderboard = MapControlCalculator.CalculateLeaderboard(
            territories.Select(territory => new ControlledTerritory(
                territory.Id,
                territory.OwnerFactionId,
                territory.AreaSquareKm)).ToArray(),
            snapshot.Factions.Select(faction =>
            {
                var current = snapshot.Leaderboard.FirstOrDefault(row => row.FactionId == faction.Id);
                return new FactionStanding(faction.Id, faction.Name, current?.EliminationCount ?? 0);
            }).ToArray());

        return snapshot with
        {
            SnapshotGeneratedAtUtc = DateTimeOffset.UtcNow,
            Territories = territories,
            Armies = armies,
            Leaderboard = leaderboard
        };
    }
}
