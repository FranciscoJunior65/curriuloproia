using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CurriculosProIA.Domain.Dtos;
using CurriculosProIA.Service.Helpers;
using CurriculosProIA.Service.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CurriculosProIA.Service.Implementations;

public class CaktoService : ICaktoService
{
    private const int PixExpiresInSeconds = 3600;

    private readonly IPaymentCheckoutService _checkout;
    private readonly IPaymentFulfillmentService _fulfillment;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CaktoService> _logger;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _accessToken;
    private DateTimeOffset _tokenExpiresAt = DateTimeOffset.MinValue;

    public CaktoService(
        IPaymentCheckoutService checkout,
        IPaymentFulfillmentService fulfillment,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<CaktoService> logger)
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

        EnsureConfigured();

        var sdkClientId = CaktoConfigHelper.GetSdkClientId(_configuration)
            ?? throw new InvalidOperationException("CAKTO_SDK_CLIENT_ID não configurado no backend/.env");

        return new CheckoutSessionResult
        {
            TransparentCheckout = true,
            AmountBRL = ctx.AmountBRL,
            PublicKey = sdkClientId,
            PixAvailable = true,
            UserId = ctx.UserId,
            PlanId = ctx.PlanId,
            PlanName = ctx.PlanName,
            Analyses = ctx.Analyses,
            CouponId = ctx.CouponInfo?.CouponId,
            CouponName = ctx.CouponInfo?.CouponName,
            DiscountPercent = ctx.CouponInfo?.DiscountPercent,
            OriginalPrice = ctx.CouponInfo?.OriginalPrice,
            CpfNormalized = ctx.CpfNormalized,
            LiveMode = true,
            PayerEmail = email
        };
    }

    public async Task<string> CreateCardTokenAsync(
        string holderName,
        string cardNumber,
        string expMonth,
        string expYear,
        string cvv,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var sdkClientId = CaktoConfigHelper.GetSdkClientId(_configuration)
            ?? throw new InvalidOperationException("CAKTO_SDK_CLIENT_ID não configurado no backend/.env");

        var month = NormalizeCardExpMonth(expMonth);
        var yearTwoDigits = NormalizeCardExpYear(expYear);
        var number = Regex.Replace(cardNumber ?? string.Empty, @"\D", string.Empty);

        if (month.Length != 2 || yearTwoDigits.Length != 2)
        {
            throw new InvalidOperationException("Validade do cartão deve usar MM e AA com 2 dígitos.");
        }

        if (number.Length < 13)
        {
            throw new InvalidOperationException("Número do cartão inválido.");
        }

        if (string.IsNullOrWhiteSpace(cvv))
        {
            throw new InvalidOperationException("CVV é obrigatório.");
        }

        // Mesmo contrato do Cakto SDK (browser): X-client-id + number + expYear com 4 dígitos.
        var body = new Dictionary<string, object>
        {
            ["holderName"] = holderName.Trim(),
            ["number"] = number,
            ["expMonth"] = month,
            ["expYear"] = ToCardTokenExpYear(yearTwoDigits),
            ["cvv"] = cvv.Trim()
        };

        var client = _httpClientFactory.CreateClient("Cakto");
        client.BaseAddress = new Uri($"{CaktoConfigHelper.BaseUrl}/");
        using var request = new HttpRequestMessage(HttpMethod.Post, "public_api/card-tokens/");
        request.Headers.TryAddWithoutValidation("X-client-id", sdkClientId);
        request.Content = JsonContent.Create(body);

        using var response = await client.SendAsync(request, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Erro Cakto ao tokenizar cartão: {json}");
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.TryGetProperty("token", out var tokenProp) && !string.IsNullOrWhiteSpace(tokenProp.GetString()))
        {
            return tokenProp.GetString()!;
        }

        if (root.TryGetProperty("cardToken", out var cardTokenProp) && !string.IsNullOrWhiteSpace(cardTokenProp.GetString()))
        {
            return cardTokenProp.GetString()!;
        }

        throw new InvalidOperationException("Cakto não retornou token do cartão.");
    }

    public async Task<string> GetThreeDsTokenAsync(
        string provider = "cielo",
        CancellationToken cancellationToken = default)
    {
        var normalizedProvider = string.IsNullOrWhiteSpace(provider)
            ? "cielo"
            : provider.Trim().ToLowerInvariant();

        var client = _httpClientFactory.CreateClient("Cakto");
        var url = $"{CaktoConfigHelper.BaseUrl}/api/financial/3ds/token/?provider={Uri.EscapeDataString(normalizedProvider)}";

        using var response = await client.GetAsync(url, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Erro Cakto ao obter token 3DS: {json}");
        }

        if (!TryExtractThreeDsAccessToken(json, out _))
        {
            throw new InvalidOperationException("Resposta Cakto 3DS sem access_token válido.");
        }

        return json;
    }

    private static bool TryExtractThreeDsAccessToken(string json, out string? accessToken)
    {
        accessToken = null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                var joined = string.Concat(root.EnumerateArray().Select(e => e.GetString() ?? string.Empty));
                using var inner = JsonDocument.Parse(joined);
                return TryReadAccessToken(inner.RootElement, out accessToken);
            }

            return TryReadAccessToken(root, out accessToken);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryReadAccessToken(JsonElement root, out string? accessToken)
    {
        accessToken = null;
        if (root.TryGetProperty("access_token", out var at) && at.ValueKind == JsonValueKind.String)
        {
            accessToken = at.GetString();
        }
        else if (root.TryGetProperty("accessToken", out var at2) && at2.ValueKind == JsonValueKind.String)
        {
            accessToken = at2.GetString();
        }
        else if (root.TryGetProperty("token", out var at3) && at3.ValueKind == JsonValueKind.String)
        {
            accessToken = at3.GetString();
        }

        return !string.IsNullOrWhiteSpace(accessToken);
    }

    public async Task<CaktoProcessPaymentResult> ProcessCardPaymentAsync(
        string planId,
        string userId,
        string email,
        string customerName,
        string cardToken,
        CaktoThreeDSecureData threeDSecure,
        string? antifraudProfilingAttemptReference = null,
        string? couponCode = null,
        string? cpf = null,
        bool includeEnglish = false,
        string? analysisId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(cardToken))
        {
            throw new InvalidOperationException("Token do cartão é obrigatório");
        }

        if (string.IsNullOrWhiteSpace(antifraudProfilingAttemptReference))
        {
            throw new InvalidOperationException(
                "Referência antifraude é obrigatória para pagamento com cartão (antifraudProfilingAttemptReference).");
        }

        var paymentCtx = await BuildPaymentContextAsync(
            planId, userId, email, customerName, couponCode, cpf, includeEnglish, analysisId, cancellationToken);

        await SyncOfferPriceAsync(paymentCtx.Ctx, cancellationToken);

        var productId = CaktoConfigHelper.GetProductId(_configuration)
            ?? throw new InvalidOperationException("CAKTO_PRODUCT_ID não configurado no backend/.env");

        var threeDsPayload = BuildThreeDSecurePayload(threeDSecure);
        var body = BuildPaymentBody(
            paymentCtx,
            "threeDs",
            card: new Dictionary<string, object> { ["token"] = cardToken.Trim() },
            threeDSecure: threeDsPayload,
            productId: productId,
            antifraudProfilingAttemptReference: antifraudProfilingAttemptReference.Trim());

        var root = await PostPaymentAsync(body, $"cakto_card_{userId}_{Guid.NewGuid():N}", cancellationToken);
        return await MapProcessPaymentResultAsync(root, cancellationToken);
    }

    public async Task<CaktoPixPaymentResult> CreatePixPaymentAsync(
        string planId,
        string userId,
        string email,
        string customerName,
        string? couponCode = null,
        string? cpf = null,
        bool includeEnglish = false,
        string? analysisId = null,
        CancellationToken cancellationToken = default)
    {
        var paymentCtx = await BuildPaymentContextAsync(
            planId, userId, email, customerName, couponCode, cpf, includeEnglish, analysisId, cancellationToken);

        await SyncOfferPriceAsync(paymentCtx.Ctx, cancellationToken);

        var body = BuildPaymentBody(
            paymentCtx,
            "pix",
            pixExpiresIn: PixExpiresInSeconds);

        var root = await PostPaymentAsync(body, $"cakto_pix_{userId}_{Guid.NewGuid():N}", cancellationToken);
        var orderId = root.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
        var status = root.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : null;

        string? qrCode = null;
        string? qrCodeBase64 = null;
        DateTimeOffset? expiration = null;

        if (root.TryGetProperty("pix", out var pix))
        {
            if (pix.TryGetProperty("qrCode", out var qr))
            {
                qrCode = qr.GetString();
            }

            if (pix.TryGetProperty("qrCodeBase64", out var qrB64))
            {
                qrCodeBase64 = qrB64.GetString();
                if (!string.IsNullOrEmpty(qrCodeBase64) && qrCodeBase64.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    var comma = qrCodeBase64.IndexOf(',');
                    if (comma >= 0)
                    {
                        qrCodeBase64 = qrCodeBase64[(comma + 1)..];
                    }
                }
            }

            if (pix.TryGetProperty("expiresAt", out var expAt)
                && DateTimeOffset.TryParse(expAt.GetString(), out var expParsed))
            {
                expiration = expParsed;
            }
            else if (pix.TryGetProperty("expirationDate", out var expDate)
                && DateTimeOffset.TryParse(expDate.GetString(), out var expDateParsed))
            {
                expiration = expDateParsed;
            }
        }

        return new CaktoPixPaymentResult
        {
            Success = !string.IsNullOrEmpty(orderId),
            PaymentId = orderId,
            Status = status,
            QrCode = qrCode,
            QrCodeBase64 = qrCodeBase64,
            Expiration = expiration,
            AmountBRL = paymentCtx.Ctx.AmountBRL,
            Message = string.IsNullOrEmpty(qrCode)
                ? "Cakto não retornou QR Code PIX."
                : null
        };
    }

    public async Task<PaymentVerificationResult> VerifyPaymentAsync(
        string orderId,
        CancellationToken cancellationToken = default)
    {
        var root = await GetOrderAsync(orderId, cancellationToken);
        var status = root.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : null;

        if (!string.Equals(status, "paid", StringComparison.OrdinalIgnoreCase))
        {
            return new PaymentVerificationResult
            {
                Paid = false,
                PaymentStatus = status
            };
        }

        var meta = ParseExternalReference(ExtractExternalReference(root));
        if (meta == null)
        {
            throw new InvalidOperationException("Pedido Cakto sem referência de checkout (sck).");
        }

        var result = await _fulfillment.FulfillPaidOrderAsync(BuildFulfillRequest(meta, orderId, root), cancellationToken);
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
            request.EnableBuffering();
            request.Body.Position = 0;

            using var doc = await JsonDocument.ParseAsync(request.Body, cancellationToken: cancellationToken);
            request.Body.Position = 0;
            var root = doc.RootElement;

            var configuredSecret = CaktoConfigHelper.GetWebhookSecret(_configuration);
            if (!string.IsNullOrWhiteSpace(configuredSecret))
            {
                var secret = root.TryGetProperty("secret", out var secretProp) ? secretProp.GetString() : null;
                if (!string.Equals(secret, configuredSecret, StringComparison.Ordinal))
                {
                    _logger.LogWarning("Webhook Cakto rejeitado: secret inválido");
                    return;
                }
            }

            var eventName = root.TryGetProperty("event", out var eventProp) ? eventProp.GetString() : null;
            if (!string.Equals(eventName, "purchase_approved", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!root.TryGetProperty("data", out var data))
            {
                return;
            }

            var orderId = data.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
            if (string.IsNullOrWhiteSpace(orderId))
            {
                return;
            }

            await VerifyPaymentAsync(orderId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro no webhook Cakto");
        }
    }

    public async Task<PaymentProviderTestResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var webhookUrl = $"{GetApiBaseUrl()}/api/analyze/payment/cakto/webhook";

        if (!CaktoConfigHelper.HasApiCredentials(_configuration))
        {
            return new PaymentProviderTestResult
            {
                Connected = false,
                Provider = "cakto",
                Message = "Defina CAKTO_CLIENT_ID e CAKTO_CLIENT_SECRET no backend/.env"
            };
        }

        try
        {
            await EnsureAccessTokenAsync(cancellationToken);
            var productId = CaktoConfigHelper.GetProductId(_configuration);
            var offerId = CaktoConfigHelper.GetOfferId(_configuration);
            var sdkClientId = CaktoConfigHelper.GetSdkClientId(_configuration);
            var hasCatalog = CaktoConfigHelper.HasCheckoutCatalog(_configuration);

            if (!hasCatalog)
            {
                return new PaymentProviderTestResult
                {
                    Connected = true,
                    Provider = "cakto",
                    Message =
                        "Conexão com Cakto OK (OAuth válido). Para cobrar, crie produto + oferta no painel e preencha CAKTO_PRODUCT_ID e CAKTO_OFFER_ID no .env.",
                    Details = new
                    {
                        webhookUrl,
                        oauthOk = true,
                        productIdConfigured = false,
                        offerIdConfigured = false,
                        sdkClientId = CaktoConfigHelper.MaskSecret(sdkClientId),
                        clientIdPreview = CaktoConfigHelper.MaskSecret(CaktoConfigHelper.GetClientId(_configuration)),
                        webhookSecretConfigured = !string.IsNullOrWhiteSpace(CaktoConfigHelper.GetWebhookSecret(_configuration))
                    }
                };
            }

            var client = CreateAuthorizedClient();
            using var offerResponse = await client.GetAsync($"public_api/offers/{offerId}/", cancellationToken);
            var offerJson = await offerResponse.Content.ReadAsStringAsync(cancellationToken);

            if (!offerResponse.IsSuccessStatusCode)
            {
                return new PaymentProviderTestResult
                {
                    Connected = false,
                    Provider = "cakto",
                    Message = $"OAuth OK, mas a oferta não foi encontrada (CAKTO_OFFER_ID={offerId}): {offerJson}",
                    Details = new { webhookUrl, productId, offerId, oauthOk = true }
                };
            }

            return new PaymentProviderTestResult
            {
                Connected = true,
                Provider = "cakto",
                Message = "Conexão com Cakto OK. Token OAuth válido e oferta acessível.",
                Details = new
                {
                    webhookUrl,
                    productId,
                    offerId,
                    oauthOk = true,
                    sdkClientId = CaktoConfigHelper.MaskSecret(sdkClientId),
                    clientIdPreview = CaktoConfigHelper.MaskSecret(CaktoConfigHelper.GetClientId(_configuration)),
                    webhookSecretConfigured = !string.IsNullOrWhiteSpace(CaktoConfigHelper.GetWebhookSecret(_configuration))
                }
            };
        }
        catch (Exception ex)
        {
            return new PaymentProviderTestResult
            {
                Connected = false,
                Provider = "cakto",
                Message = ex.Message,
                Details = new { webhookUrl }
            };
        }
    }

    private async Task SyncOfferPriceAsync(CheckoutContext ctx, CancellationToken cancellationToken)
    {
        var offerId = CaktoConfigHelper.GetOfferId(_configuration)!;
        var client = await CreateAuthorizedClientAsync(cancellationToken);

        var body = new
        {
            name = TruncatePlanName(ctx.PlanName),
            price = (double)ctx.AmountBRL,
            status = "active"
        };

        using var response = await client.PutAsJsonAsync($"public_api/offers/{offerId}/", body, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Erro ao sincronizar preço da oferta Cakto: {json}");
        }
    }

    private Dictionary<string, object> BuildPaymentBody(
        CaktoPaymentContext paymentCtx,
        string paymentMethod,
        Dictionary<string, object>? card = null,
        Dictionary<string, object>? threeDSecure = null,
        int? pixExpiresIn = null,
        string? productId = null,
        string? antifraudProfilingAttemptReference = null)
    {
        var offerId = CaktoConfigHelper.GetOfferId(_configuration)!;

        var body = new Dictionary<string, object>
        {
            ["paymentMethod"] = paymentMethod,
            ["customer"] = BuildCustomer(paymentCtx),
            ["items"] = new[]
            {
                new Dictionary<string, object>
                {
                    ["offerId"] = offerId,
                    ["quantity"] = 1,
                    ["offerType"] = "main"
                }
            },
            ["metadata"] = new Dictionary<string, string>
            {
                ["sck"] = paymentCtx.ExternalReference
            }
        };

        if (!string.IsNullOrWhiteSpace(productId))
        {
            body["productId"] = productId;
        }

        if (!string.IsNullOrWhiteSpace(antifraudProfilingAttemptReference))
        {
            body["antifraudProfilingAttemptReference"] = antifraudProfilingAttemptReference;
        }

        if (card != null)
        {
            body["card"] = card;
        }

        if (threeDSecure != null && threeDSecure.Count > 0)
        {
            body["threeDSecure"] = threeDSecure;
        }

        if (pixExpiresIn.HasValue)
        {
            body["pixExpiresIn"] = pixExpiresIn.Value;
        }

        return body;
    }

    private Dictionary<string, object> BuildCustomer(CaktoPaymentContext paymentCtx)
    {
        var customer = new Dictionary<string, object>
        {
            ["name"] = paymentCtx.CustomerName,
            ["email"] = paymentCtx.Email,
            ["phone"] = paymentCtx.Phone,
            ["fingerprint"] = paymentCtx.Fingerprint
        };

        if (!string.IsNullOrWhiteSpace(paymentCtx.CpfNormalized))
        {
            customer["docType"] = "cpf";
            customer["docNumber"] = paymentCtx.CpfNormalized;
        }

        return customer;
    }

    private static Dictionary<string, object> BuildThreeDSecurePayload(CaktoThreeDSecureData threeDSecure)
    {
        var payload = new Dictionary<string, object>();

        if (!string.IsNullOrWhiteSpace(threeDSecure.Cavv))
        {
            payload["cavv"] = threeDSecure.Cavv;
        }

        if (!string.IsNullOrWhiteSpace(threeDSecure.Eci))
        {
            payload["eci"] = threeDSecure.Eci;
        }

        if (!string.IsNullOrWhiteSpace(threeDSecure.Xid))
        {
            payload["xid"] = threeDSecure.Xid;
        }

        if (!string.IsNullOrWhiteSpace(threeDSecure.ReferenceId))
        {
            payload["referenceId"] = threeDSecure.ReferenceId;
        }

        if (!string.IsNullOrWhiteSpace(threeDSecure.Version))
        {
            payload["version"] = threeDSecure.Version;
        }

        if (!string.IsNullOrWhiteSpace(threeDSecure.TransStatus))
        {
            payload["trans_status"] = threeDSecure.TransStatus;
        }

        if (!string.IsNullOrWhiteSpace(threeDSecure.TdsServerTransId))
        {
            payload["tds_server_trans_id"] = threeDSecure.TdsServerTransId;
        }

        return payload;
    }

    private async Task<JsonElement> PostPaymentAsync(
        Dictionary<string, object> body,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var client = await CreateAuthorizedClientAsync(cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, "public_api/payments/");
        request.Headers.TryAddWithoutValidation("X-Idempotency-Key", idempotencyKey);
        request.Content = JsonContent.Create(body);

        using var response = await client.SendAsync(request, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Erro Cakto ao criar pagamento: {json}");
        }

        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private async Task<JsonElement> GetOrderAsync(string orderId, CancellationToken cancellationToken)
    {
        var client = await CreateAuthorizedClientAsync(cancellationToken);
        using var response = await client.GetAsync($"public_api/orders/{orderId}/", cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Erro ao buscar pedido Cakto: {json}");
        }

        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private async Task<CaktoProcessPaymentResult> MapProcessPaymentResultAsync(
        JsonElement root,
        CancellationToken cancellationToken)
    {
        var orderId = root.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
        var status = root.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : null;

        if (string.Equals(status, "paid", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(orderId))
        {
            var verify = await VerifyPaymentAsync(orderId, cancellationToken);
            return new CaktoProcessPaymentResult
            {
                Success = true,
                Paid = true,
                PaymentId = orderId,
                Status = status,
                User = verify.User,
                AlreadyFulfilled = verify.AlreadyFulfilled
            };
        }

        return new CaktoProcessPaymentResult
        {
            Success = !string.IsNullOrEmpty(orderId),
            Paid = false,
            PaymentId = orderId,
            Status = status,
            Message = status is "waiting_payment" or "pending"
                ? "Pagamento em processamento."
                : "Pagamento não aprovado."
        };
    }

    private async Task<CaktoPaymentContext> BuildPaymentContextAsync(
        string planId,
        string userId,
        string email,
        string customerName,
        string? couponCode,
        string? cpf,
        bool includeEnglish,
        string? analysisId,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();

        var ctx = await _checkout.BuildCheckoutContextAsync(
            planId, userId, couponCode, cpf, includeEnglish, analysisId, cancellationToken);

        var cpfNormalized = ctx.CpfNormalized ?? ctx.Metadata.GetValueOrDefault("cpfNormalized");
        var resolvedName = string.IsNullOrWhiteSpace(customerName)
            ? ctx.PlanName
            : customerName.Trim();
        var resolvedEmail = string.IsNullOrWhiteSpace(email) ? $"{userId}@curriculoproia.local" : email.Trim();

        return new CaktoPaymentContext(
            ctx,
            BuildExternalReference(ctx),
            resolvedName,
            resolvedEmail,
            cpfNormalized,
            $"fp_{userId}",
            "5511000000000");
    }

    private static string BuildExternalReference(CheckoutContext ctx)
    {
        var payload = new CaktoExternalReference
        {
            U = ctx.UserId,
            P = ctx.PlanId,
            N = TruncatePlanName(ctx.PlanName),
            A = ctx.Analyses,
            M = ctx.AmountBRL,
            C = ctx.CouponInfo?.CouponId,
            D = ctx.CouponInfo?.DiscountPercent,
            O = ctx.CouponInfo?.OriginalPrice,
            F = ctx.Metadata.GetValueOrDefault("cpfNormalized"),
            E = ctx.Metadata.GetValueOrDefault("includeEnglish") == "true",
            G = ctx.Metadata.TryGetValue("englishPriceBRL", out var ep)
                && decimal.TryParse(ep, NumberStyles.Any, CultureInfo.InvariantCulture, out var epv)
                ? epv
                : null,
            I = ctx.Metadata.GetValueOrDefault("analysisId")
        };

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        if (json.Length <= 255)
        {
            return json;
        }

        payload.N = TruncatePlanName(ctx.PlanId);
        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private static string? ExtractExternalReference(JsonElement order)
    {
        if (order.TryGetProperty("metadata", out var metadata)
            && metadata.TryGetProperty("sck", out var sck))
        {
            return sck.GetString();
        }

        if (order.TryGetProperty("sck", out var directSck))
        {
            return directSck.GetString();
        }

        return null;
    }

    private static CaktoExternalReference? ParseExternalReference(string? externalReference)
    {
        if (string.IsNullOrWhiteSpace(externalReference))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<CaktoExternalReference>(externalReference, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static FulfillOrderRequest BuildFulfillRequest(
        CaktoExternalReference meta,
        string orderId,
        JsonElement order)
    {
        decimal price = meta.M;
        if (order.TryGetProperty("amount", out var amountProp))
        {
            var amountStr = amountProp.GetString();
            if (decimal.TryParse(amountStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            {
                price = parsed;
            }
        }

        var email = order.TryGetProperty("customer", out var customer)
            && customer.TryGetProperty("email", out var emailProp)
            ? emailProp.GetString() ?? string.Empty
            : string.Empty;

        return new FulfillOrderRequest
        {
            UserId = meta.U ?? string.Empty,
            PlanId = meta.P ?? string.Empty,
            PlanName = meta.N ?? $"Plano {meta.P}",
            Analyses = meta.A,
            Price = price,
            PaymentMethod = "cakto",
            PaymentId = orderId,
            CustomerEmail = email,
            CouponId = meta.C,
            DiscountPercent = meta.D,
            OriginalPrice = meta.O,
            CpfNormalized = meta.F,
            IncludeEnglish = meta.E,
            EnglishPriceBRL = meta.G ?? 0,
            AnalysisId = meta.I
        };
    }

    private async Task<HttpClient> CreateAuthorizedClientAsync(CancellationToken cancellationToken)
    {
        await EnsureAccessTokenAsync(cancellationToken);
        return CreateAuthorizedClient();
    }

    private HttpClient CreateAuthorizedClient()
    {
        var client = _httpClientFactory.CreateClient("Cakto");
        client.BaseAddress = new Uri($"{CaktoConfigHelper.BaseUrl}/");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        return client;
    }

    private async Task EnsureAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_accessToken) && DateTimeOffset.UtcNow < _tokenExpiresAt.AddMinutes(-5))
        {
            return;
        }

        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            if (!string.IsNullOrEmpty(_accessToken) && DateTimeOffset.UtcNow < _tokenExpiresAt.AddMinutes(-5))
            {
                return;
            }

            var clientId = CaktoConfigHelper.GetClientId(_configuration)
                ?? throw new InvalidOperationException("CAKTO_CLIENT_ID não configurado");
            var clientSecret = CaktoConfigHelper.GetClientSecret(_configuration)
                ?? throw new InvalidOperationException("CAKTO_CLIENT_SECRET não configurado");

            var client = _httpClientFactory.CreateClient("Cakto");
            client.BaseAddress = new Uri($"{CaktoConfigHelper.BaseUrl}/");
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret
            });

            using var response = await client.PostAsync("public_api/token/", content, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Erro ao autenticar na Cakto: {json}");
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            _accessToken = root.GetProperty("access_token").GetString();
            var expiresIn = root.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600;
            _tokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn);
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private void EnsureConfigured()
    {
        if (!CaktoConfigHelper.IsConfigured(_configuration))
        {
            throw new InvalidOperationException(CaktoConfigHelper.BuildMissingConfigMessage(_configuration));
        }
    }

    private string GetApiBaseUrl()
    {
        var configured = _configuration["PUBLIC_API_URL"]?.Trim().TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        return "http://localhost:3000";
    }

    private static string NormalizeCardExpMonth(string value)
    {
        var digits = Regex.Replace(value ?? string.Empty, @"\D", string.Empty);
        if (digits.Length == 0)
        {
            return string.Empty;
        }

        return digits.Length > 2 ? digits[^2..] : digits.PadLeft(2, '0');
    }

    private static string NormalizeCardExpYear(string value)
    {
        var digits = Regex.Replace(value ?? string.Empty, @"\D", string.Empty);
        if (digits.Length == 0)
        {
            return string.Empty;
        }

        if (digits.Length >= 4)
        {
            return digits[^2..];
        }

        return digits.PadLeft(2, '0');
    }

    private static string ToCardTokenExpYear(string yearTwoDigits)
    {
        var century = DateTime.UtcNow.Year.ToString()[..2];
        return $"{century}{yearTwoDigits}";
    }

    private static string TruncatePlanName(string value) =>
        value.Length <= 40 ? value : value[..40];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private sealed record CaktoPaymentContext(
        CheckoutContext Ctx,
        string ExternalReference,
        string CustomerName,
        string Email,
        string? CpfNormalized,
        string Fingerprint,
        string Phone);

    private sealed class CaktoExternalReference
    {
        public string? U { get; set; }
        public string? P { get; set; }
        public string? N { get; set; }
        public int A { get; set; }
        public decimal M { get; set; }
        public string? C { get; set; }
        public decimal? D { get; set; }
        public decimal? O { get; set; }
        public string? F { get; set; }
        public bool E { get; set; }
        public decimal? G { get; set; }
        public string? I { get; set; }
    }
}
