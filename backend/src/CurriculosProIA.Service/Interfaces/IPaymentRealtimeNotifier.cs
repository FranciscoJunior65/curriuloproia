using CurriculosProIA.Domain.Dtos;

namespace CurriculosProIA.Service.Interfaces;

public interface IPaymentRealtimeNotifier
{
    Task NotifyPaymentConfirmedAsync(
        PaymentConfirmedNotification notification,
        CancellationToken cancellationToken = default);
}
