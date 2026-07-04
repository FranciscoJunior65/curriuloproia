using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CurriculosProIA.Api.Hubs;

/// <summary>
/// Hub em tempo real: o front conecta com JWT e entra no grupo do usuário.
/// O webhook Kiwify notifica via <see cref="Infrastructure.PaymentRealtimeNotifier"/>.
/// </summary>
[Authorize]
public class PaymentHub : Hub
{
    public const string PaymentConfirmedEvent = "paymentConfirmed";

    public static string UserGroup(string userId) => $"user:{userId}";

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (!string.IsNullOrWhiteSpace(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (!string.IsNullOrWhiteSpace(userId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, UserGroup(userId));
        }

        await base.OnDisconnectedAsync(exception);
    }

    private string? GetUserId() =>
        Context.User?.FindFirst("userId")?.Value
        ?? Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
}
