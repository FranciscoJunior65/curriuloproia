using Microsoft.AspNetCore.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CurriculosProIA.Domain.Entities;
using CurriculosProIA.Domain.Dtos;
using CurriculosProIA.Repository.Persistence;
using CurriculosProIA.Domain.Dtos;

using CurriculosProIA.Service.Helpers;
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
    private readonly ISettingsService _settings;
    private readonly ILogger<MercadoPagoService> _logger;
    private bool? _cachedLiveMode;
    private string? _cachedMode;

    public MercadoPagoService(
        IPaymentCheckoutService checkout,
        IPaymentFulfillmentService fulfillment,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ISettingsService settings,
        ILogger<MercadoPagoService> logger)
    {
        _checkout = checkout;
        _fulfillment = fulfillment;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _settings = settings;
        _logger = logger;
    }

    public async Task<CheckoutSessionResult> CreateCheckoutAsync(
        string planId,
        string userId,
        string email,
        string? frontendUrl = null,
        string? couponCode = null,
        string? cpf = null,
        bool includeEnglish = false,
        string? analysisId = null,
        CancellationToken cancellationToken = default)
    {
        var ctx = await _checkout.BuildCheckoutContextAsync(
            planId, userId, couponCode, cpf, includeEnglish, analysisId, cancellationToken);

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
            amountBRL = ctx.AmountBRL,
            includeEnglish = ctx.Metadata.GetValueOrDefault("includeEnglish") == "true",
            englishPriceBRL = ctx.Metadata.TryGetValue("englishPriceBRL", out var ep) &&
                decimal.TryParse(ep, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var epv)
                ? epv
                : 0m,
            analysisId = ctx.Metadata.GetValueOrDefault("analysisId")
        });

        var successUrl = PaymentReturnUrls.Build(
            baseUrl, PaymentReturnUrls.SuccessPath, "mercadopago", userId,
            ctx.Metadata.GetValueOrDefault("analysisId"),
            englishPaid: planId == "english");
        var failureUrl = PaymentReturnUrls.Build(
            baseUrl, PaymentReturnUrls.FailurePath, "mercadopago", userId,
            ctx.Metadata.GetValueOrDefault("analysisId"));
        var pendingUrl = PaymentReturnUrls.Build(
            baseUrl, PaymentReturnUrls.PendingPath, "mercadopago", userId,
            ctx.Metadata.GetValueOrDefault("analysisId"));

        var isProduction = await ResolveIsProductionModeAsync(cancellationToken);

        var body = new Dictionary<string, object?>
        {
            ["items"] = new[]
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
            ["external_reference"] = externalReference,
            ["metadata"] = ctx.Metadata,
            ["back_urls"] = new Dictionary<string, string>
            {
                ["success"] = successUrl,
                ["failure"] = failureUrl,
                ["pending"] = pendingUrl
            },
            ["payment_methods"] = MercadoPagoConfigHelper.BuildCheckoutPaymentMethods(isProduction)
        };

        var cpfNormalized = ctx.CpfNormalized ?? ctx.Metadata.GetValueOrDefault("cpfNormalized");
        var payer = BuildPayer(email, cpfNormalized);
        if (payer != null)
        {
            body["payer"] = payer;
        }

        // MP rejeita auto_return com localhost — só em produção/ngrok HTTPS
        if (PaymentReturnUrls.SupportsMercadoPagoHttpsCallback(successUrl))
        {
            body["auto_return"] = "approved";
        }

        var notificationUrl = $"{GetApiBaseUrl()}/api/analyze/payment/mercadopago/webhook";
        if (PaymentReturnUrls.SupportsMercadoPagoHttpsCallback(notificationUrl))
        {
            body["notification_url"] = notificationUrl;
        }
        else
        {
            _logger.LogWarning(
                "notification_url omitida (localhost ou HTTP). Webhook automático indisponível em dev local.");
        }

        var client = await CreateClientAsync(cancellationToken);
        using var response = await client.PostAsJsonAsync("checkout/preferences", body, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Erro Mercado Pago: {json}");
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var initPoint = ResolveCheckoutInitPoint(root, isProduction);

        if (string.IsNullOrEmpty(initPoint))
        {
            throw new InvalidOperationException("Mercado Pago não retornou URL de checkout");
        }

        _logger.LogInformation(
            "Checkout Mercado Pago criado: modo={Mode}, url={CheckoutMode}",
            isProduction ? "production" : "sandbox",
            initPoint.Contains("sandbox", StringComparison.OrdinalIgnoreCase) ? "sandbox_init_point" : "init_point");

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
        var client = await CreateClientAsync(cancellationToken);
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
            CpfNormalized = meta?.CpfNormalized,
            IncludeEnglish = meta?.IncludeEnglish ?? false,
            EnglishPriceBRL = meta?.EnglishPriceBRL ?? 0,
            AnalysisId = meta?.AnalysisId
        }, cancellationToken);

        return new PaymentVerificationResult
        {
            Paid = true,
            User = result.User,
            AlreadyFulfilled = result.AlreadyFulfilled
        };
    }

    public async Task HandleWebhookAsync(HttpRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var (topic, id) = await ParseWebhookNotificationAsync(request, cancellationToken);

            if (topic == "payment" && !string.IsNullOrEmpty(id))
            {
                await VerifyPaymentAsync(id, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro no webhook Mercado Pago");
        }
    }

    private static async Task<(string? Topic, string? Id)> ParseWebhookNotificationAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var query = request.Query;
        var topic = query["topic"].FirstOrDefault() ?? query["type"].FirstOrDefault();
        var id = query["id"].FirstOrDefault() ?? query["data.id"].FirstOrDefault();

        if (!string.IsNullOrEmpty(topic) && !string.IsNullOrEmpty(id))
        {
            return (topic, id);
        }

        if (request.ContentLength is null or 0)
        {
            return (topic, id);
        }

        request.EnableBuffering();
        request.Body.Position = 0;

        try
        {
            using var doc = await JsonDocument.ParseAsync(request.Body, cancellationToken: cancellationToken);
            var root = doc.RootElement;

            topic ??= root.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : null;
            topic ??= root.TryGetProperty("action", out var actionProp) ? actionProp.GetString() : null;

            if (string.IsNullOrEmpty(id)
                && root.TryGetProperty("data", out var data)
                && data.TryGetProperty("id", out var dataId))
            {
                id = dataId.ValueKind == JsonValueKind.String
                    ? dataId.GetString()
                    : dataId.GetRawText();
            }
        }
        catch (JsonException)
        {
            // Corpo vazio ou não-JSON — query string já foi considerada acima.
        }
        finally
        {
            request.Body.Position = 0;
        }

        return (topic, id);
    }

    public async Task<PaymentProviderTestResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var configuredMode = await ResolveModeAsync(cancellationToken);
        var token = MercadoPagoConfigHelper.GetAccessToken(_configuration, configuredMode);

        if (string.IsNullOrWhiteSpace(token))
        {
            return new PaymentProviderTestResult
            {
                Connected = false,
                Provider = "mercadopago",
                Message = "Credencial Mercado Pago não configurada. Defina MERCADOPAGO_MODE e os tokens _TEST/_PRODUCTION no .env"
            };
        }

        var webhookUrl = $"{GetApiBaseUrl()}/api/analyze/payment/mercadopago/webhook";
        var frontendUrl = _configuration["FRONTEND_URL"]?.TrimEnd('/') ?? "http://localhost:4200";

        try
        {
            var client = await CreateClientAsync(cancellationToken);
            using var response = await client.GetAsync("users/me", cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new PaymentProviderTestResult
                {
                    Connected = false,
                    Provider = "mercadopago",
                    Message = json,
                    Details = new { webhookUrl, tokenPreview = MercadoPagoConfigHelper.MaskToken(token), configuredMode }
                };
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var liveMode = root.TryGetProperty("live_mode", out var lm) && lm.GetBoolean();
            _cachedLiveMode = liveMode;
            var mode = liveMode ? "production" : "test";
            var checkoutTarget = liveMode ? "init_point" : "sandbox_init_point";
            var paymentMethods = await GetAvailablePaymentMethodsAsync(client, cancellationToken);
            var pixAvailable = paymentMethods.Any(m =>
                string.Equals(m, "pix", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m, "bank_transfer", StringComparison.OrdinalIgnoreCase));

            return new PaymentProviderTestResult
            {
                Connected = true,
                Provider = "mercadopago",
                Message = pixAvailable
                    ? $"Conexão com Mercado Pago OK (modo {mode}). PIX disponível na conta."
                    : $"Conexão com Mercado Pago OK (modo {mode}). PIX não encontrado nos meios da conta — verifique chave PIX.",
                Details = new
                {
                    mode,
                    liveMode,
                    checkoutTarget,
                    pixAvailable,
                    paymentMethods,
                    userId = root.TryGetProperty("id", out var id) ? id.GetRawText() : null,
                    email = root.TryGetProperty("email", out var email) ? email.GetString() : null,
                    country = root.TryGetProperty("country_id", out var country) ? country.GetString() : null,
                    siteId = root.TryGetProperty("site_id", out var site) ? site.GetString() : null,
                    tokenPreview = MercadoPagoConfigHelper.MaskToken(token),
                    webhookUrl,
                    webhookConfigured = PaymentReturnUrls.SupportsMercadoPagoHttpsCallback(webhookUrl),
                    frontendUrl,
                    paymentProvider = _configuration["PAYMENT_PROVIDER"] ?? "stripe",
                    configuredMode,
                    config = MercadoPagoConfigHelper.GetDebugInfo(_configuration, configuredMode),
                    pixHint = pixAvailable
                        ? "Se PIX não aparecer no checkout, use sandbox_init_point e informe CPF do pagador."
                        : "Cadastre uma chave PIX em mercadopago.com.br → Seu negócio → Chaves PIX."
                }
            };
        }
        catch (Exception ex)
        {
            return new PaymentProviderTestResult
            {
                Connected = false,
                Provider = "mercadopago",
                Message = ex.Message,
                Details = new { webhookUrl, tokenPreview = MercadoPagoConfigHelper.MaskToken(token), configuredMode }
            };
        }
    }

    private async Task<string> ResolveModeAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_cachedMode))
        {
            return _cachedMode;
        }

        _cachedMode = await _settings.GetMercadoPagoModeAsync(cancellationToken);
        return _cachedMode;
    }

    private async Task<bool> ResolveIsProductionModeAsync(CancellationToken cancellationToken)
    {
        if (!_cachedLiveMode.HasValue)
        {
            var mode = await ResolveModeAsync(cancellationToken);
            _cachedLiveMode = mode == MercadoPagoConfigHelper.ModeProduction;
        }

        return _cachedLiveMode.Value;
    }

    private static string? ResolveCheckoutInitPoint(JsonElement root, bool isProduction)
    {
        var production = root.TryGetProperty("init_point", out var ip) ? ip.GetString() : null;
        var sandbox = root.TryGetProperty("sandbox_init_point", out var sip) ? sip.GetString() : null;
        return isProduction ? production ?? sandbox : sandbox ?? production;
    }

    private static object? BuildPayer(string? email, string? cpfNormalized)
    {
        var hasEmail = IsValidEmail(email);
        var hasCpf = !string.IsNullOrWhiteSpace(cpfNormalized) && cpfNormalized.Length == 11;

        if (!hasEmail && !hasCpf)
        {
            return null;
        }

        var payer = new Dictionary<string, object?>();
        if (hasEmail)
        {
            payer["email"] = email!.Trim();
        }

        if (hasCpf)
        {
            payer["identification"] = new { type = "CPF", number = cpfNormalized };
        }

        return payer;
    }

    private static async Task<IReadOnlyList<string>> GetAvailablePaymentMethodsAsync(
        HttpClient client,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.GetAsync("v1/payment_methods", cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return Array.Empty<string>();
            }

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<string>();
            }

            var methods = new List<string>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (item.TryGetProperty("id", out var id))
                {
                    methods.Add(id.GetString() ?? string.Empty);
                }

                if (item.TryGetProperty("payment_type_id", out var typeId))
                {
                    var type = typeId.GetString();
                    if (!string.IsNullOrEmpty(type) && !methods.Contains(type))
                    {
                        methods.Add(type);
                    }
                }
            }

            return methods
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(m => m, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private async Task<HttpClient> CreateClientAsync(CancellationToken cancellationToken)
    {
        var mode = await ResolveModeAsync(cancellationToken);
        var token = MercadoPagoConfigHelper.GetAccessToken(_configuration, mode)
            ?? throw new InvalidOperationException(
                "Mercado Pago não configurado. Defina os tokens _TEST/_PRODUCTION no .env");

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
        public bool IncludeEnglish { get; set; }
        public decimal EnglishPriceBRL { get; set; }
        public string? AnalysisId { get; set; }
    }
}
