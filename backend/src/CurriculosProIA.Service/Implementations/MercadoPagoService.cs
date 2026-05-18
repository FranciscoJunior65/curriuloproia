using Microsoft.AspNetCore.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CurriculosProIA.Domain.Entities;
using CurriculosProIA.Domain.Dtos;
using CurriculosProIA.Repository.Persistence;
using CurriculosProIA.Domain.Dtos;

using CurriculosProIA.Service.Interfaces;
using CurriculosProIA.Repository.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CurriculosProIA.Service.Implementations;

public class MercadoPagoService : IMercadoPagoService
{
    private readonly IPaymentCheckoutService _checkout;
    private readonly IPaymentFulfillmentService _fulfillment;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MercadoPagoService> _logger;

    public MercadoPagoService(
        IPaymentCheckoutService checkout,
        IPaymentFulfillmentService fulfillment,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<MercadoPagoService> logger)
    {
        _checkout = checkout;
        _fulfillment = fulfillment;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<CheckoutSessionResult> CreateCheckoutAsync(
        string planId,
        string userId,
        string email,
        string? frontendUrl = null,
        string? couponCode = null,
        string? cpf = null,
        CancellationToken cancellationToken = default)
    {
        var ctx = await _checkout.BuildCheckoutContextAsync(planId, userId, couponCode, cpf, cancellationToken);

        if (ctx.FreeCheckout)
        {
            return new CheckoutSessionResult
            {
                FreeCheckout = true,
                UserId = ctx.UserId,
                PlanId = ctx.PlanId,
                PlanName = ctx.PlanName,
                Analyses = ctx.Analyses,
                CouponId = ctx.CouponInfo?.CouponId,
                CouponName = ctx.CouponInfo?.CouponName,
                DiscountPercent = ctx.CouponInfo?.DiscountPercent,
                OriginalPrice = ctx.CouponInfo?.OriginalPrice,
                CpfNormalized = ctx.CpfNormalized
            };
        }

        var baseUrl = (frontendUrl ?? _configuration["FRONTEND_URL"] ?? "http://localhost:4200").TrimEnd('/');
        var externalReference = JsonSerializer.Serialize(new
        {
            userId = ctx.UserId,
            planId = ctx.PlanId,
            planName = ctx.PlanName,
            analyses = ctx.Analyses,
            couponId = ctx.Metadata.GetValueOrDefault("couponId"),
            couponName = ctx.Metadata.GetValueOrDefault("couponName"),
            discountPercent = ctx.Metadata.GetValueOrDefault("discountPercent") != null
                ? decimal.Parse(ctx.Metadata["discountPercent"], System.Globalization.CultureInfo.InvariantCulture)
                : (decimal?)null,
            originalPrice = ctx.Metadata.GetValueOrDefault("originalPrice") != null
                ? decimal.Parse(ctx.Metadata["originalPrice"], System.Globalization.CultureInfo.InvariantCulture)
                : (decimal?)null,
            cpfNormalized = ctx.Metadata.GetValueOrDefault("cpfNormalized"),
            amountBRL = ctx.AmountBRL
        });

        var body = new
        {
            items = new[]
            {
                new
                {
                    id = ctx.PlanId,
                    title = ctx.PlanName,
                    description = ctx.Plan.Description + (ctx.CouponInfo != null
                        ? $" ({ctx.CouponInfo.CouponName}: {ctx.CouponInfo.DiscountPercent}% off)"
                        : string.Empty),
                    quantity = 1,
                    unit_price = ctx.AmountBRL,
                    currency_id = "BRL"
                }
            },
            payer = IsValidEmail(email) ? new { email = email.Trim() } : null,
            external_reference = externalReference,
            metadata = ctx.Metadata,
            back_urls = new
            {
                success = $"{baseUrl}?provider=mercadopago&userId={userId}",
                failure = $"{baseUrl}?provider=mercadopago&status=failure&userId={userId}",
                pending = $"{baseUrl}?provider=mercadopago&status=pending&userId={userId}"
            },
            auto_return = "approved",
            notification_url = $"{GetApiBaseUrl()}/api/analyze/payment/mercadopago/webhook"
        };

        var client = CreateClient();
        using var response = await client.PostAsJsonAsync("checkout/preferences", body, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Erro Mercado Pago: {json}");
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var initPoint = root.TryGetProperty("init_point", out var ip) ? ip.GetString()
            : root.TryGetProperty("sandbox_init_point", out var sip) ? sip.GetString() : null;

        if (string.IsNullOrEmpty(initPoint))
        {
            throw new InvalidOperationException("Mercado Pago não retornou URL de checkout");
        }

        var preferenceId = root.GetProperty("id").GetString();
        return new CheckoutSessionResult
        {
            SessionId = preferenceId,
            Url = initPoint,
            PreferenceId = preferenceId
        };
    }

    public async Task<PaymentVerificationResult> VerifyPaymentAsync(string paymentId, CancellationToken cancellationToken = default)
    {
        var client = CreateClient();
        using var response = await client.GetAsync($"v1/payments/{paymentId}", cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Erro ao buscar pagamento Mercado Pago: {json}");
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var status = root.GetProperty("status").GetString();

        var meta = ParseExternalReference(
            root.TryGetProperty("external_reference", out var er) ? er.GetString() : null);

        if (status != "approved")
        {
            return new PaymentVerificationResult
            {
                Paid = false,
                PaymentStatus = status,
                StatusDetail = root.TryGetProperty("status_detail", out var sd) ? sd.GetString() : null
            };
        }

        var userId = meta?.UserId ?? string.Empty;
        var planId = meta?.PlanId ?? string.Empty;
        var analyses = meta?.Analyses ?? 0;
        var planName = meta?.PlanName ?? $"Plano {planId}";
        var price = meta?.AmountBRL ?? (root.TryGetProperty("transaction_amount", out var ta) ? ta.GetDecimal() : 0);

        var result = await _fulfillment.FulfillPaidOrderAsync(new FulfillOrderRequest
        {
            UserId = userId,
            PlanId = planId,
            PlanName = planName,
            Analyses = analyses,
            Price = price,
            PaymentMethod = "mercadopago",
            PaymentId = paymentId,
            CustomerEmail = root.TryGetProperty("payer", out var payer) && payer.TryGetProperty("email", out var em)
                ? em.GetString() ?? string.Empty
                : string.Empty,
            CouponId = meta?.CouponId,
            CouponName = meta?.CouponName,
            DiscountPercent = meta?.DiscountPercent,
            OriginalPrice = meta?.OriginalPrice,
            CpfNormalized = meta?.CpfNormalized
        }, cancellationToken);

        return new PaymentVerificationResult
        {
            Paid = true,
            User = result.User,
            AlreadyFulfilled = result.AlreadyFulfilled
        };
    }

    public async Task HandleWebhookAsync(IQueryCollection query, CancellationToken cancellationToken = default)
    {
        try
        {
            var topic = query["topic"].FirstOrDefault() ?? query["type"].FirstOrDefault();
            var id = query["id"].FirstOrDefault() ?? query["data.id"].FirstOrDefault();

            if ((topic == "payment" || topic == "merchant_order") && !string.IsNullOrEmpty(id) && topic == "payment")
            {
                await VerifyPaymentAsync(id, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro no webhook Mercado Pago");
        }
    }

    public async Task<PaymentProviderTestResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var token = _configuration["MERCADOPAGO_ACCESS_TOKEN"];
        if (string.IsNullOrWhiteSpace(token))
        {
            return new PaymentProviderTestResult
            {
                Connected = false,
                Provider = "mercadopago",
                Message = "MERCADOPAGO_ACCESS_TOKEN não configurado no .env"
            };
        }

        try
        {
            var client = CreateClient();
            using var response = await client.GetAsync("users/me", cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new PaymentProviderTestResult
                {
                    Connected = false,
                    Provider = "mercadopago",
                    Message = json
                };
            }

            using var doc = JsonDocument.Parse(json);
            var mode = token.Contains("TEST", StringComparison.Ordinal) ? "test" : "production";
            return new PaymentProviderTestResult
            {
                Connected = true,
                Provider = "mercadopago",
                Message = $"Conexão com Mercado Pago OK (modo {mode}).",
                Details = new
                {
                    mode,
                    userId = doc.RootElement.TryGetProperty("id", out var id) ? id.GetRawText() : null
                }
            };
        }
        catch (Exception ex)
        {
            return new PaymentProviderTestResult
            {
                Connected = false,
                Provider = "mercadopago",
                Message = ex.Message
            };
        }
    }

    private HttpClient CreateClient()
    {
        var token = _configuration["MERCADOPAGO_ACCESS_TOKEN"]
            ?? throw new InvalidOperationException("MERCADOPAGO_ACCESS_TOKEN não configurado");

        var client = _httpClientFactory.CreateClient("MercadoPago");
        client.BaseAddress = new Uri("https://api.mercadopago.com/");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private string GetApiBaseUrl()
    {
        var url = _configuration["PUBLIC_API_URL"] ?? _configuration["API_URL"]
            ?? $"http://localhost:{_configuration["PORT"] ?? "3000"}";
        return url.TrimEnd('/');
    }

    private static bool IsValidEmail(string email) =>
        !string.IsNullOrWhiteSpace(email) &&
        System.Text.RegularExpressions.Regex.IsMatch(email.Trim(), @"^[^\s@]+@[^\s@]+\.[^\s@]+$");

    private static MpExternalReference? ParseExternalReference(string? externalReference)
    {
        if (string.IsNullOrEmpty(externalReference))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<MpExternalReference>(externalReference,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return null;
        }
    }

    private sealed class MpExternalReference
    {
        public string? UserId { get; set; }
        public string? PlanId { get; set; }
        public string? PlanName { get; set; }
        public int Analyses { get; set; }
        public string? CouponId { get; set; }
        public string? CouponName { get; set; }
        public decimal? DiscountPercent { get; set; }
        public decimal? OriginalPrice { get; set; }
        public string? CpfNormalized { get; set; }
        public decimal AmountBRL { get; set; }
    }
}
