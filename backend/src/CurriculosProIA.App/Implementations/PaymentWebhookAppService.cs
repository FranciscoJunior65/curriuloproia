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
    private readonly ICaktoService _cakto;
    private readonly IKiwifyService _kiwify;

    public PaymentWebhookAppService(IStripePaymentService stripe, IMercadoPagoService mercadoPago,
        ICaktoService cakto,
        IKiwifyService kiwify,
        IHttpContextAccessor http)
    {
        _stripe = stripe;
        _mercadoPago = mercadoPago;
        _cakto = cakto;
        _kiwify = kiwify;
        _http = http;
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
        await _mercadoPago.HandleWebhookAsync(_http.HttpContext!.Request, cancellationToken);
        return Content("OK", "text/plain");
    }

    public async Task<IActionResult> CaktoWebhook(CancellationToken cancellationToken)
    {
        await _cakto.HandleWebhookAsync(_http.HttpContext!.Request, cancellationToken);
        return Ok(new { received = true });
    }

    public async Task<IActionResult> KiwifyWebhook(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _kiwify.HandleWebhookAsync(_http.HttpContext!.Request, cancellationToken);
            return Ok(new
            {
                received = true,
                processed = result?.Paid == true,
                alreadyFulfilled = result?.AlreadyFulfilled == true,
                credits = result?.User?.Credits,
                userId = result?.User?.Id
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { received = false, error = ex.Message });
        }
    }
}
