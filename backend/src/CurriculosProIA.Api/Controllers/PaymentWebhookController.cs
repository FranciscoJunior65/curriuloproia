using CurriculosProIA.App.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CurriculosProIA.Api.Controllers;

[ApiController]
[Route("api/analyze/payment")]
public class PaymentWebhookController : ControllerBase
{
    private readonly IPaymentWebhookAppService _webhooks;

    public PaymentWebhookController(IPaymentWebhookAppService webhooks) => _webhooks = webhooks;

    [HttpPost("webhook")]
    public Task<IActionResult> StripeWebhook(CancellationToken ct) => _webhooks.StripeWebhook(ct);

    [HttpPost("mercadopago/webhook")]
    public Task<IActionResult> MercadoPagoWebhook(CancellationToken ct) => _webhooks.MercadoPagoWebhook(ct);
}
