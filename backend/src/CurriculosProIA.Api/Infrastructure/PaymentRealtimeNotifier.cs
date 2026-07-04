using CurriculosProIA.Api.Hubs;
using CurriculosProIA.Domain.Dtos;
using CurriculosProIA.Service.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace CurriculosProIA.Api.Infrastructure;

public class PaymentRealtimeNotifier : IPaymentRealtimeNotifier
{
    private readonly IHubContext<PaymentHub> _hub;
    private readonly ILogger<PaymentRealtimeNotifier> _logger;

    public PaymentRealtimeNotifier(IHubContext<PaymentHub> hub, ILogger<PaymentRealtimeNotifier> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public async Task NotifyPaymentConfirmedAsync(
        PaymentConfirmedNotification notification,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(notification.UserId))
        {
            return;
        }

        var payload = new
        {
            userId = notification.UserId,
            credits = notification.Credits,
            orderId = notification.OrderId,
            planId = notification.PlanId,
            provider = notification.Provider,
            alreadyFulfilled = notification.AlreadyFulfilled
        };

        await _hub.Clients
            .Group(PaymentHub.UserGroup(notification.UserId))
            .SendAsync(PaymentHub.PaymentConfirmedEvent, payload, cancellationToken);

        _logger.LogInformation(
            "SignalR paymentConfirmed enviado user={UserId} credits={Credits} order={OrderId}",
            notification.UserId,
            notification.Credits,
            notification.OrderId);
    }
}
