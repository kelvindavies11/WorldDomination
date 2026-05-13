namespace Game.Domain;

public sealed record NeutralCaptureRequest(
    string ActingFactionId,
    string SourceTerritoryId,
    string? SourceOwnerFactionId,
    string TargetTerritoryId,
    string? TargetOwnerFactionId,
    int AvailableArmyStrength,
    int RequestedStrength,
    bool HasAllowedRoute);

public sealed record NeutralCaptureValidationResult(
    bool IsValid,
    string? Error)
{
    public static NeutralCaptureValidationResult Valid() => new(true, null);

    public static NeutralCaptureValidationResult Invalid(string error) => new(false, error);
}

public sealed record NeutralCaptureArmyState(
    string ActingFactionId,
    int SourceArmyStrength,
    int RequestedStrength);

public sealed record NeutralCaptureResult(
    string TargetOwnerFactionId,
    int SourceArmyStrength,
    int TargetArmyStrength);

public sealed record TerritoryTakeoverRequest(
    string ActingFactionId,
    string SourceTerritoryId,
    string? SourceOwnerFactionId,
    string TargetTerritoryId,
    string? TargetOwnerFactionId,
    int AvailableArmyStrength,
    int RequestedStrength,
    bool HasAllowedRoute);

public sealed record TerritoryTakeoverValidationResult(
    bool IsValid,
    string? Error)
{
    public static TerritoryTakeoverValidationResult Valid() => new(true, null);

    public static TerritoryTakeoverValidationResult Invalid(string error) => new(false, error);
}

public sealed record TerritoryTakeoverArmyState(
    string ActingFactionId,
    string? TargetOwnerFactionId,
    int SourceArmyStrength,
    int RequestedStrength,
    int DefenderArmyStrength,
    int TerritoryDefense,
    int AttackPosition = 0,
    int DefensePosition = 0);

public sealed record ReinforcementRequest(
    string ActingFactionId,
    string SourceTerritoryId,
    string? SourceOwnerFactionId,
    string TargetTerritoryId,
    string? TargetOwnerFactionId,
    int AvailableArmyStrength,
    int RequestedStrength,
    bool HasAllowedRoute);

public sealed record ReinforcementValidationResult(
    bool IsValid,
    string? Error)
{
    public static ReinforcementValidationResult Valid() => new(true, null);

    public static ReinforcementValidationResult Invalid(string error) => new(false, error);
}

public sealed record ReinforcementArmyState(
    int SourceArmyStrength,
    int RequestedStrength,
    int TargetArmyStrength);

public sealed record ReinforcementResult(
    int SourceArmyStrength,
    int TargetArmyStrength);

public sealed record TerritoryTakeoverResult(
    string? TargetOwnerFactionId,
    int SourceArmyStrength,
    int TargetArmyStrength,
    CombatResult? Combat);

public static class TerritoryExpansion
{
    public static TerritoryTakeoverValidationResult ValidateTakeover(TerritoryTakeoverRequest request)
    {
        if (request.SourceOwnerFactionId != request.ActingFactionId)
        {
            return TerritoryTakeoverValidationResult.Invalid("Source territory is not owned by the acting faction.");
        }

        if (request.AvailableArmyStrength <= 0)
        {
            return TerritoryTakeoverValidationResult.Invalid("Source territory has no army.");
        }

        if (request.RequestedStrength < 1)
        {
            return TerritoryTakeoverValidationResult.Invalid("Strength must be at least 1.");
        }

        if (request.RequestedStrength > request.AvailableArmyStrength)
        {
            return TerritoryTakeoverValidationResult.Invalid("Strength cannot exceed the available source army strength.");
        }

        if (!request.HasAllowedRoute)
        {
            return TerritoryTakeoverValidationResult.Invalid("Target territory is not connected to the source.");
        }

        if (request.TargetOwnerFactionId == request.ActingFactionId)
        {
            return TerritoryTakeoverValidationResult.Invalid("Target territory is already owned by the acting faction.");
        }

        return TerritoryTakeoverValidationResult.Valid();
    }

    public static NeutralCaptureValidationResult ValidateNeutralCapture(NeutralCaptureRequest request)
    {
        var takeoverValidation = ValidateTakeover(new TerritoryTakeoverRequest(
            request.ActingFactionId,
            request.SourceTerritoryId,
            request.SourceOwnerFactionId,
            request.TargetTerritoryId,
            request.TargetOwnerFactionId,
            request.AvailableArmyStrength,
            request.RequestedStrength,
            request.HasAllowedRoute));

        if (!takeoverValidation.IsValid)
        {
            return NeutralCaptureValidationResult.Invalid(takeoverValidation.Error ?? "Movement command is invalid.");
        }

        if (request.TargetOwnerFactionId is not null)
        {
            return NeutralCaptureValidationResult.Invalid("Target territory is not neutral in this first slice.");
        }

        return NeutralCaptureValidationResult.Valid();
    }

    public static NeutralCaptureResult ApplyNeutralCapture(NeutralCaptureArmyState state) =>
        new(
            TargetOwnerFactionId: state.ActingFactionId,
            SourceArmyStrength: state.SourceArmyStrength - state.RequestedStrength,
            TargetArmyStrength: state.RequestedStrength);

    public static ReinforcementValidationResult ValidateReinforcement(ReinforcementRequest request)
    {
        if (request.SourceOwnerFactionId != request.ActingFactionId)
            return ReinforcementValidationResult.Invalid("Source territory is not owned by the acting faction.");

        if (request.TargetOwnerFactionId != request.ActingFactionId)
            return ReinforcementValidationResult.Invalid("Target territory is not owned by the acting faction.");

        if (request.SourceTerritoryId == request.TargetTerritoryId)
            return ReinforcementValidationResult.Invalid("Source and target must be different territories.");

        if (request.AvailableArmyStrength <= 1)
            return ReinforcementValidationResult.Invalid("Source territory must keep at least one troop.");

        if (request.RequestedStrength < 1)
            return ReinforcementValidationResult.Invalid("Strength must be at least 1.");

        if (request.RequestedStrength > request.AvailableArmyStrength - 1)
            return ReinforcementValidationResult.Invalid("You must leave at least 1 troop in the source territory.");

        if (!request.HasAllowedRoute)
            return ReinforcementValidationResult.Invalid("Target territory is not connected to the source.");

        return ReinforcementValidationResult.Valid();
    }

    public static ReinforcementResult ApplyReinforcement(ReinforcementArmyState state) =>
        new(
            SourceArmyStrength: state.SourceArmyStrength - state.RequestedStrength,
            TargetArmyStrength: state.TargetArmyStrength + state.RequestedStrength);

    public static TerritoryTakeoverResult ResolveTakeover(TerritoryTakeoverArmyState state)
    {
        if (state.TargetOwnerFactionId is null)
        {
            return new TerritoryTakeoverResult(
                TargetOwnerFactionId: state.ActingFactionId,
                SourceArmyStrength: state.SourceArmyStrength - state.RequestedStrength,
                TargetArmyStrength: state.RequestedStrength,
                Combat: null);
        }

        var combat = CombatCalculator.Resolve(new CombatInput(
            AttackerStrength: state.RequestedStrength,
            DefenderStrength: state.DefenderArmyStrength,
            TerritoryDefense: state.TerritoryDefense,
            AttackPosition: state.AttackPosition,
            DefensePosition: state.DefensePosition));

        return combat.Winner == CombatWinner.Attacker
            ? new TerritoryTakeoverResult(
                TargetOwnerFactionId: state.ActingFactionId,
                SourceArmyStrength: state.SourceArmyStrength - state.RequestedStrength,
                TargetArmyStrength: combat.SurvivingStrength,
                Combat: combat)
            : new TerritoryTakeoverResult(
                TargetOwnerFactionId: state.TargetOwnerFactionId,
                SourceArmyStrength: state.SourceArmyStrength - state.RequestedStrength,
                TargetArmyStrength: combat.SurvivingStrength,
                Combat: combat);
    }
}
