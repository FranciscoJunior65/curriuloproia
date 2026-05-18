using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using CurriculosProIA.Repository.Interfaces;
using CurriculosProIA.Service.Interfaces;
using CurriculosProIA.Domain.Entities;
using CurriculosProIA.Domain.Dtos;
using CurriculosProIA.Domain.Signatures.Auth;
using CurriculosProIA.Domain.Signatures.Analyze;
using CurriculosProIA.Domain.Signatures.Admin;
using CurriculosProIA.Domain.Signatures.Purchase;
using CurriculosProIA.App.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CurriculosProIA.App.Implementations;
using CurriculosProIA.App;
using CurriculosProIA.App.Interfaces;

public class PaymentWebhookAppService : AppControllerBase, IPaymentWebhookAppService 
{
    private readonly IHttpContextAccessor _http;
    private readonly IStripePaymentService _stripe;
    private readonly IMercadoPagoService _mercadoPago;

    public PaymentWebhookAppService(IStripePaymentService stripe, IMercadoPagoService mercadoPago,
        IHttpContextAccessor http)
    {
        _stripe = stripe;
        _mercadoPago = mercadoPago;
    }

        public async Task<IActionResult> StripeWebhook(CancellationToken cancellationToken)
    {
        var signature = _http.HttpContext!.Request.Headers["Stripe-Signature"].FirstOrDefault() ?? string.Empty;
        using var reader = new MemoryStream();
        await _http.HttpContext!.Request.Body.CopyToAsync(reader, cancellationToken);
        var rawBody = reader.ToArray();

        try
        {
            await _stripe.HandleWebhookAsync(rawBody, signature, cancellationToken);
            return Ok(new { received = true });
        }
        catch (Exception ex)
        {
            return BadRequest($"Webhook Error: {ex.Message}");
        }
    }

        public async Task<IActionResult> MercadoPagoWebhook(CancellationToken cancellationToken)
    {
        await _mercadoPago.HandleWebhookAsync(_http.HttpContext!.Request.Query, cancellationToken);
        return Content("OK", "text/plain");
    }
}
