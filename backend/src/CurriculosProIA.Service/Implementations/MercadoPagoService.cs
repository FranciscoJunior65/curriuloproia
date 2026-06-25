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
    private const string SandboxTestCpf = "12345678909";

    private readonly IPaymentCheckoutService _checkout;
    private readonly IPaymentFulfillmentService _fulfillment;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ISettingsService _settings;
    private readonly ILogger<MercadoPagoService> _logger;

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

        var mpCtx = await BuildPaymentContextAsync(
            planId, userId, email, couponCode, cpf, includeEnglish, analysisId, cancellationToken);

        var mode = await ResolveModeAsync(cancellationToken);
        var publicKey = MercadoPagoConfigHelper.GetPublicKey(_configuration, mode)
            ?? throw new InvalidOperationException("MERCADOPAGO_PUBLIC_KEY não configurada no backend/.env");

        _logger.LogInformation(
            "Checkout transparente Mercado Pago preparado: planId={PlanId}, amount={Amount}, liveMode={LiveMode}, pix={Pix}",
            mpCtx.Ctx.PlanId,
            mpCtx.Ctx.AmountBRL,
            mpCtx.TokenLiveMode,
            mpCtx.TokenLiveMode);

        return new CheckoutSessionResult
        {
            TransparentCheckout = true,
            AmountBRL = mpCtx.Ctx.AmountBRL,
            PublicKey = publicKey,
            PixAvailable = mpCtx.TokenLiveMode,
            UserId = mpCtx.Ctx.UserId,
            PlanId = mpCtx.Ctx.PlanId,
            PlanName = mpCtx.Ctx.PlanName,
            Analyses = mpCtx.Ctx.Analyses,
            CouponId = mpCtx.Ctx.CouponInfo?.CouponId,
            CouponName = mpCtx.Ctx.CouponInfo?.CouponName,
            DiscountPercent = mpCtx.Ctx.CouponInfo?.DiscountPercent,
            OriginalPrice = mpCtx.Ctx.CouponInfo?.OriginalPrice,
            CpfNormalized = mpCtx.CpfNormalized,
            LiveMode = mpCtx.TokenLiveMode,
            PayerEmail = mpCtx.PayerEmail ?? email
        };
    }

    public async Task<MercadoPagoProcessPaymentResult> ProcessCardPaymentAsync(
        string planId,
        string userId,
        string email,
        string cardToken,
        string paymentMethodId,
        string? issuerId,
        int installments,
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

        if (string.IsNullOrWhiteSpace(paymentMethodId))
        {
            throw new InvalidOperationException("payment_method_id é obrigatório");
        }

        var mpCtx = await BuildPaymentContextAsync(
            planId, userId, email, couponCode, cpf, includeEnglish, analysisId, cancellationToken);

        var body = new Dictionary<string, object>
        {
            ["transaction_amount"] = mpCtx.Ctx.AmountBRL,
            ["token"] = cardToken.Trim(),
            ["description"] = mpCtx.Ctx.PlanName,
            ["installments"] = installments < 1 ? 1 : installments,
            ["payment_method_id"] = paymentMethodId.Trim(),
            ["external_reference"] = mpCtx.ExternalReference,
            ["metadata"] = mpCtx.Ctx.Metadata,
            ["statement_descriptor"] = "CURRICULOSPRO IA"
        };

        var payer = BuildPayer(mpCtx.PayerEmail, mpCtx.CpfNormalized);
        if (payer != null)
        {
            body["payer"] = payer;
        }

        if (!string.IsNullOrWhiteSpace(issuerId))
        {
            body["issuer_id"] = issuerId.Trim();
        }

        AppendNotificationUrl(body);

        var root = await PostPaymentAsync(body, $"card_{userId}_{Guid.NewGuid():N}", cancellationToken);
        return await MapProcessPaymentResultAsync(root, cancellationToken);
    }

    public async Task<MercadoPagoPixPaymentResult> CreatePixPaymentAsync(
        string planId,
        string userId,
        string email,
        string? couponCode = null,
        string? cpf = null,
        bool includeEnglish = false,
        string? analysisId = null,
        CancellationToken cancellationToken = default)
    {
        var mpCtx = await BuildPaymentContextAsync(
            planId, userId, email, couponCode, cpf, includeEnglish, analysisId, cancellationToken);

        if (!mpCtx.TokenLiveMode)
        {
            return new MercadoPagoPixPaymentResult
            {
                Success = false,
                Message = "PIX disponível apenas em produção (MERCADOPAGO_MODE=production). No sandbox use cartão de teste."
            };
        }

        var body = new Dictionary<string, object>
        {
            ["transaction_amount"] = mpCtx.Ctx.AmountBRL,
            ["description"] = mpCtx.Ctx.PlanName,
            ["payment_method_id"] = "pix",
            ["external_reference"] = mpCtx.ExternalReference,
            ["metadata"] = mpCtx.Ctx.Metadata
        };

        var payer = BuildPayer(mpCtx.PayerEmail, mpCtx.CpfNormalized);
        if (payer != null)
        {
            body["payer"] = payer;
        }

        AppendNotificationUrl(body);

        var root = await PostPaymentAsync(body, $"pix_{userId}_{Guid.NewGuid():N}", cancellationToken);
        var paymentId = root.TryGetProperty("id", out var idProp) ? idProp.GetRawText().Trim('"') : null;
        var status = root.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : null;

        string? qrCode = null;
        string? qrCodeBase64 = null;
        string? ticketUrl = null;
        DateTimeOffset? expiration = null;

        if (root.TryGetProperty("point_of_interaction", out var poi)
            && poi.TryGetProperty("transaction_data", out var txData))
        {
            if (txData.TryGetProperty("qr_code", out var qr))
            {
                qrCode = qr.GetString();
            }

            if (txData.TryGetProperty("qr_code_base64", out var qrB64))
            {
                qrCodeBase64 = qrB64.GetString();
            }

            if (txData.TryGetProperty("ticket_url", out var ticket))
            {
                ticketUrl = ticket.GetString();
            }
        }

        if (root.TryGetProperty("date_of_expiration", out var expProp)
            && DateTimeOffset.TryParse(expProp.GetString(), out var exp))
        {
            expiration = exp;
        }

        return new MercadoPagoPixPaymentResult
        {
            Success = !string.IsNullOrEmpty(paymentId),
            PaymentId = paymentId,
            Status = status,
            QrCode = qrCode,
            QrCodeBase64 = qrCodeBase64,
            TicketUrl = ticketUrl,
            Expiration = expiration,
            AmountBRL = mpCtx.Ctx.AmountBRL,
            Message = string.IsNullOrEmpty(qrCode)
                ? "Mercado Pago não retornou QR Code PIX. Verifique chave PIX na conta."
                : null
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
            var mode = liveMode ? "production" : "test";
            var checkoutTarget = liveMode ? "init_point" : "sandbox_init_point";
            var modeAligned = (configuredMode == MercadoPagoConfigHelper.ModeProduction) == liveMode;
            var paymentMethods = await GetAvailablePaymentMethodsAsync(client, cancellationToken);
            var pixAvailable = paymentMethods.Any(m =>
                string.Equals(m, "pix", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m, "bank_transfer", StringComparison.OrdinalIgnoreCase));

            string message;
            if (!modeAligned)
            {
                message = configuredMode == MercadoPagoConfigHelper.ModeProduction
                    ? "CONFIGURAÇÃO INVÁLIDA: modo produção ativo (Painel Admin ou MERCADOPAGO_MODE=production), mas o token é de TESTE. " +
                      "Cole o token de produção em MERCADOPAGO_ACCESS_TOKEN_PRODUCTION e republique, " +
                      "ou defina modo teste no admin / MERCADOPAGO_MODE=test."
                    : "CONFIGURAÇÃO INVÁLIDA: modo teste ativo (Painel Admin ou MERCADOPAGO_MODE=test), mas o token é de PRODUÇÃO. " +
                      "Use MERCADOPAGO_ACCESS_TOKEN_TEST e republique.";
            }
            else
            {
                message = pixAvailable
                    ? $"Conexão com Mercado Pago OK (modo {mode}). PIX disponível na conta."
                    : $"Conexão com Mercado Pago OK (modo {mode}). PIX não encontrado nos meios da conta — verifique chave PIX.";
            }

            return new PaymentProviderTestResult
            {
                Connected = modeAligned,
                Provider = "mercadopago",
                Message = message,
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
                    modeAligned,
                    config = MercadoPagoConfigHelper.GetDebugInfo(_configuration, configuredMode),
                    sandboxHint = !liveMode
                        ? "Sandbox: use cartão de teste (5031 4332 1540 6351), titular APRO, CPF 12345678909. PIX desabilitado no sandbox."
                        : null,
                    pixHint = !liveMode
                        ? "PIX só funciona em produção com token e chave PIX reais."
                        : pixAvailable
                            ? "Pague como convidado (não use a conta vendedor dgomesoliveira81@gmail.com)."
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

    private async Task<string> ResolveModeAsync(CancellationToken cancellationToken) =>
        await _settings.GetMercadoPagoModeAsync(cancellationToken);

    private async Task<bool> ResolveIsProductionModeAsync(CancellationToken cancellationToken)
    {
        var mode = await ResolveModeAsync(cancellationToken);
        return MercadoPagoConfigHelper.IsProductionMode(_configuration, mode);
    }

    private string? ResolvePayerEmail(string? email, bool isProduction)
    {
        if (isProduction)
        {
            return email;
        }

        var testPayer = _configuration["MERCADOPAGO_TEST_PAYER_EMAIL"]?.Trim();
        if (!string.IsNullOrEmpty(testPayer) && IsValidEmail(testPayer))
        {
            return testPayer;
        }

        if (IsValidEmail(email)
            && email!.Trim().EndsWith("@testuser.com", StringComparison.OrdinalIgnoreCase))
        {
            return email.Trim();
        }

        return null;
    }

    private static string? ResolveCheckoutInitPoint(JsonElement root)
    {
        var production = root.TryGetProperty("init_point", out var ip) ? ip.GetString() : null;
        var sandbox = root.TryGetProperty("sandbox_init_point", out var sip) ? sip.GetString() : null;
        var liveMode = root.TryGetProperty("live_mode", out var lm) && lm.GetBoolean();

        // Usa live_mode retornado pela preferência (reflete o token real), não só o admin.
        return liveMode ? production ?? sandbox : sandbox ?? production;
    }

    private static void EnsureTokenModeAlignedAsync(bool configuredProduction, bool tokenLiveMode, CancellationToken _)
    {
        if (configuredProduction && !tokenLiveMode)
        {
            throw new InvalidOperationException(
                "Mercado Pago: modo produção ativo (Painel Admin → Pagamentos ou MERCADOPAGO_MODE=production no .env), " +
                "mas MERCADOPAGO_ACCESS_TOKEN_PRODUCTION é um token de TESTE. Cole o Access Token de produção no backend/.env e republique, " +
                "ou defina modo teste no admin / MERCADOPAGO_MODE=test para sandbox (cartão 5031 4332 1540 6351).");
        }

        if (!configuredProduction && tokenLiveMode)
        {
            throw new InvalidOperationException(
                "Mercado Pago: modo teste ativo (Painel Admin → Pagamentos ou MERCADOPAGO_MODE=test no .env), " +
                "mas MERCADOPAGO_ACCESS_TOKEN_TEST é um token de PRODUÇÃO. Use o token de teste no backend/.env e republique.");
        }
    }

    private async Task<bool> ResolveTokenLiveModeAsync(CancellationToken cancellationToken)
    {
        var client = await CreateClientAsync(cancellationToken);
        using var response = await client.GetAsync("users/me", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("live_mode", out var lm) && lm.GetBoolean();
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

    private async Task<MpPaymentContext> BuildPaymentContextAsync(
        string planId,
        string userId,
        string email,
        string? couponCode,
        string? cpf,
        bool includeEnglish,
        string? analysisId,
        CancellationToken cancellationToken)
    {
        var ctx = await _checkout.BuildCheckoutContextAsync(
            planId, userId, couponCode, cpf, includeEnglish, analysisId, cancellationToken);

        var configuredProduction = await ResolveIsProductionModeAsync(cancellationToken);
        var tokenLiveMode = await ResolveTokenLiveModeAsync(cancellationToken);
        EnsureTokenModeAlignedAsync(configuredProduction, tokenLiveMode, cancellationToken);

        var payerEmail = ResolvePayerEmail(email, tokenLiveMode);
        var cpfNormalized = ctx.CpfNormalized ?? ctx.Metadata.GetValueOrDefault("cpfNormalized");
        if (!tokenLiveMode)
        {
            cpfNormalized = SandboxTestCpf;
            if (string.IsNullOrEmpty(payerEmail))
            {
                _logger.LogWarning(
                    "Sandbox MP: MERCADOPAGO_TEST_PAYER_EMAIL não definido. Use e-mail @testuser.com e cartão 5031 4332 1540 6351.");
            }
        }

        return new MpPaymentContext(
            ctx,
            BuildExternalReferenceJson(ctx),
            payerEmail,
            cpfNormalized,
            tokenLiveMode);
    }

    private static string BuildExternalReferenceJson(CheckoutContext ctx) =>
        JsonSerializer.Serialize(new
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

    private void AppendNotificationUrl(Dictionary<string, object> body)
    {
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
    }

    private async Task<JsonElement> PostPaymentAsync(
        Dictionary<string, object> body,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var client = await CreateClientAsync(cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/payments");
        request.Headers.TryAddWithoutValidation("X-Idempotency-Key", idempotencyKey);
        request.Content = JsonContent.Create(body);

        using var response = await client.SendAsync(request, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Erro Mercado Pago ao criar pagamento: {json}");
        }

        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private async Task<MercadoPagoProcessPaymentResult> MapProcessPaymentResultAsync(
        JsonElement root,
        CancellationToken cancellationToken)
    {
        var paymentId = root.TryGetProperty("id", out var idProp) ? idProp.GetRawText().Trim('"') : null;
        var status = root.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : null;
        var statusDetail = root.TryGetProperty("status_detail", out var sdProp) ? sdProp.GetString() : null;

        if (status == "approved")
        {
            var verify = await VerifyPaymentAsync(paymentId ?? string.Empty, cancellationToken);
            return new MercadoPagoProcessPaymentResult
            {
                Success = true,
                Paid = true,
                PaymentId = paymentId,
                Status = status,
                StatusDetail = statusDetail,
                User = verify.User,
                AlreadyFulfilled = verify.AlreadyFulfilled
            };
        }

        var rejected = status is "rejected" or "cancelled";
        return new MercadoPagoProcessPaymentResult
        {
            Success = !rejected,
            Paid = false,
            PaymentId = paymentId,
            Status = status,
            StatusDetail = statusDetail,
            Message = rejected
                ? MapStatusDetailMessage(statusDetail)
                : status == "in_process" || status == "pending"
                    ? "Pagamento em processamento. Aguarde a confirmação."
                    : null
        };
    }

    private static string? MapStatusDetailMessage(string? statusDetail) =>
        statusDetail switch
        {
            "cc_rejected_insufficient_amount" => "Saldo insuficiente no cartão.",
            "cc_rejected_bad_filled_security_code" => "CVV inválido.",
            "cc_rejected_bad_filled_date" => "Data de validade inválida.",
            "cc_rejected_bad_filled_card_number" => "Número do cartão inválido.",
            "cc_rejected_call_for_authorize" => "Entre em contato com o banco para autorizar a compra.",
            "cc_rejected_high_risk" => "Pagamento recusado por segurança.",
            _ => statusDetail != null ? $"Pagamento recusado ({statusDetail})." : "Pagamento recusado."
        };

    private sealed record MpPaymentContext(
        CheckoutContext Ctx,
        string ExternalReference,
        string? PayerEmail,
        string? CpfNormalized,
        bool TokenLiveMode);

    private async Task<HttpClient> CreateClientAsync(CancellationToken cancellationToken)
    {
        var mode = await ResolveModeAsync(cancellationToken);
        var token = MercadoPagoConfigHelper.GetAccessToken(_configuration, mode)
            ?? throw new InvalidOperationException(
                MercadoPagoConfigHelper.BuildMissingTokenMessage(mode));

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
