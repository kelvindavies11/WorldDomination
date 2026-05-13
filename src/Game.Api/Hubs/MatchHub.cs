using Microsoft.AspNetCore.SignalR;

namespace Game.Api.Hubs;

/// <summary>
/// SignalR hub that clients connect to for real-time match and lobby updates.
/// Clients join a group named after their game ID to receive targeted broadcasts.
/// </summary>
public sealed class MatchHub : Hub
{
    public async Task JoinMatchGroup(string gameId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(gameId));
    }

    public async Task LeaveMatchGroup(string gameId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(gameId));
    }

    public static string GroupName(string gameId) => $"match:{gameId.Trim().ToLowerInvariant()}";
}
