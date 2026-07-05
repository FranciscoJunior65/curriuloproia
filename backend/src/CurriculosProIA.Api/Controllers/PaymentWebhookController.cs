using CurriculosProIA.App.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CurriculosProIA.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/analyze/payment")]
public class PaymentWebhookController : ControllerBase
{
    private readonly IPaymentWebhookAppService _webhooks;

    public PaymentWebhookController(IPaymentWebhookAppService webhooks) => _webhooks = webhooks;

    [HttpPost("webhook")]
    public Task<IActionResult> StripeWebhook(CancellationToken ct) => _webhooks.StripeWebhook(ct);

    /// <summary>IPN/Webhook do Mercado Pago (POST JSON ou query string).</summary>
    [HttpPost("mercadopago/webhook")]
    public Task<IActionResult> MercadoPagoWebhook(CancellationToken ct) => _webhooks.MercadoPagoWebhook(ct);

    /// <summary>IPN legado do Mercado Pago (GET com topic e id na query).</summary>
    [HttpGet("mercadopago/webhook")]
    public Task<IActionResult> MercadoPagoWebhookGet(CancellationToken ct) => _webhooks.MercadoPagoWebhook(ct);

    [HttpPost("cakto/webhook")]
    public Task<IActionResult> CaktoWebhook(CancellationToken ct) => _webhooks.CaktoWebhook(ct);

    [HttpPost("kiwify/webhook")]
    public Task<IActionResult> KiwifyWebhook(CancellationToken ct) =>
        _webhooks.KiwifyWebhook(ct);
}
