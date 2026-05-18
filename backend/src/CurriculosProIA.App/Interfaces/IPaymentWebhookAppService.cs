using Microsoft.AspNetCore.Mvc;

namespace CurriculosProIA.App.Interfaces;

public interface IPaymentWebhookAppService
{
    Task<IActionResult> StripeWebhook(CancellationToken cancellationToken = default);
    Task<IActionResult> MercadoPagoWebhook(CancellationToken cancellationToken = default);
}
