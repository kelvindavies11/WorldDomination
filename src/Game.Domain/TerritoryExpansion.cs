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

public static class TerritoryExpansion
{
    public static NeutralCaptureValidationResult ValidateNeutralCapture(NeutralCaptureRequest request)
    {
        if (request.SourceOwnerFactionId != request.ActingFactionId)
        {
            return NeutralCaptureValidationResult.Invalid("Source territory is not owned by the acting faction.");
        }

        if (request.AvailableArmyStrength <= 0)
        {
            return NeutralCaptureValidationResult.Invalid("Source territory has no army.");
        }

        if (request.RequestedStrength < 1)
        {
            return NeutralCaptureValidationResult.Invalid("Strength must be at least 1.");
        }

        if (request.RequestedStrength > request.AvailableArmyStrength)
        {
            return NeutralCaptureValidationResult.Invalid("Strength cannot exceed the available source army strength.");
        }

        if (!request.HasAllowedRoute)
        {
            return NeutralCaptureValidationResult.Invalid("Target territory is not connected to the source.");
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
}
