using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CurriculosProIA.Domain.Dtos;
using CurriculosProIA.Domain.Signatures.Analyze;
using CurriculosProIA.Repository.Interfaces;
using CurriculosProIA.Service.Helpers;
using CurriculosProIA.Service.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
namespace CurriculosProIA.Service.Implementations;

public class KiwifyService : IKiwifyService
{
    private const string DefaultCustomerPhone = "5511999999999";

    private readonly IPaymentCheckoutService _checkout;
    private readonly IPaymentFulfillmentService _fulfillment;
    private readonly IPaymentRealtimeNotifier _realtimeNotifier;
    private readonly IPurchaseRepository _purchases;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<KiwifyService> _logger;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _accessToken;
    private DateTimeOffset _tokenExpiresAt = DateTimeOffset.MinValue;

    public KiwifyService(
        IPaymentCheckoutService checkout,
        IPaymentFulfillmentService fulfillment,
        IPaymentRealtimeNotifier realtimeNotifier,
        IPurchaseRepository purchases,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<KiwifyService> logger)
    {
        _checkout = checkout;
        _fulfillment = fulfillment;
        _realtimeNotifier = realtimeNotifier;
        _purchases = purchases;
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

        // Inglês desativado na Kiwify: cada link pay.kiwify tem preço fixo (sem bundle/sync).
        if (string.Equals(planId, "english", StringComparison.OrdinalIgnoreCase) || includeEnglish)
        {
            throw new InvalidOperationException(
                "Currículo em inglês temporariamente indisponível na Kiwify. Use os planos single, pack3 ou pack5.");
        }

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

        var checkoutCode = KiwifyConfigHelper.GetCheckoutCode(_configuration, planId, includeEnglish);
        if (string.IsNullOrWhiteSpace(checkoutCode))
        {
            var envHint = includeEnglish
                ? $"KIWIFY_CHECKOUT_{planId.ToUpperInvariant()}_ENGLISH"
                : $"KIWIFY_CHECKOUT_{planId.ToUpperInvariant()}";
            throw new InvalidOperationException(
                $"Link de checkout Kiwify não configurado. Defina {envHint} no backend/.env.");
        }

        var customerName = string.IsNullOrWhiteSpace(email)
            ? ctx.PlanName
            : email.Trim().Split('@')[0];

        var checkoutUrl = BuildHostedCheckoutUrl(
            checkoutCode,
            customerName,
            email.Trim(),
            ctx.CpfNormalized,
            couponCode,
            BuildExternalReference(ctx));

        await RegisterPendingPurchaseSafeAsync(ctx, cancellationToken);

        return new CheckoutSessionResult
        {
            TransparentCheckout = false,
            Url = checkoutUrl,
            AmountBRL = ctx.AmountBRL,
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

    public async Task<PaymentVerificationResult> VerifyPaymentAsync(
        string orderId,
        CancellationToken cancellationToken = default,
        JsonElement? webhookPayload = null)
    {
        JsonElement sale;
        var webhookOrder = webhookPayload.HasValue ? GetWebhookBody(webhookPayload.Value) : default;
        var hasWebhookOrder = webhookOrder.ValueKind == JsonValueKind.Object;

        if (hasWebhookOrder)
        {
            var webhookStatus = ReadString(webhookOrder, "order_status", "status");
            if (!IsPaidStatus(webhookStatus))
            {
                return new PaymentVerificationResult
                {
                    Paid = false,
                    PaymentStatus = webhookStatus
                };
            }

            sale = webhookOrder;
            try
            {
                sale = await GetSaleAsync(orderId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Kiwify: consulta v1/sales/{OrderId} falhou; usando dados do webhook",
                    orderId);
            }
        }
        else
        {
            sale = await GetSaleAsync(orderId, cancellationToken);
            var status = ReadString(sale, "status", "order_status");
            if (!IsPaidStatus(status))
            {
                return new PaymentVerificationResult
                {
                    Paid = false,
                    PaymentStatus = status
                };
            }
        }

        var sck = ResolveExternalReference(sale, webhookPayload);
        var meta = ParseExternalReference(sck);
        if (meta == null || string.IsNullOrWhiteSpace(meta.U))
        {
            throw new InvalidOperationException(
                $"Venda Kiwify {orderId} sem referência de checkout (sck) com userId. " +
                "Confirme que o checkout foi aberto pelo app (parâmetro sck na URL).");
        }

        var paymentId = ResolvePaymentId(sale, webhookOrder, orderId);

        _logger.LogInformation(
            "Kiwify: liberando créditos order={OrderId} paymentId={PaymentId} user={UserId} plan={PlanId} analyses={Analyses}",
            orderId,
            paymentId,
            meta.U,
            meta.P,
            meta.A);

        var result = await _fulfillment.FulfillPaidOrderAsync(
            BuildFulfillRequest(meta, paymentId, sale, webhookPayload),
            cancellationToken);

        var verifyResult = new PaymentVerificationResult
        {
            Paid = true,
            User = result.User,
            AlreadyFulfilled = result.AlreadyFulfilled
        };

        if (!result.AlreadyFulfilled)
        {
            await NotifyPaymentConfirmedSafeAsync(
                meta.U,
                result.User?.Credits ?? 0,
                paymentId,
                meta.P,
                false,
                cancellationToken);
        }

        return verifyResult;
    }

    private async Task NotifyPaymentConfirmedSafeAsync(
        string userId,
        int credits,
        string orderId,
        string? planId,
        bool alreadyFulfilled,
        CancellationToken cancellationToken)
    {
        try
        {
            await _realtimeNotifier.NotifyPaymentConfirmedAsync(
                new PaymentConfirmedNotification
                {
                    UserId = userId,
                    Credits = credits,
                    OrderId = orderId,
                    PlanId = planId,
                    Provider = "kiwify",
                    AlreadyFulfilled = alreadyFulfilled
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao notificar hub de pagamento (user={UserId})", userId);
        }
    }

    public async Task<KiwifyWebhookHandleResult> HandleWebhookAsync(
        KiwifyWebhookSignature body,
        string? rawBody = null,
        CancellationToken cancellationToken = default,
        string? queryToken = null)
    {
        var result = new KiwifyWebhookHandleResult();

        if (body == null)
        {
            result.FailureStage = "payload_vazio";
            result.FailureMessage = "Webhook recebido sem payload.";
            return result;
        }

        var bodyForAuth = string.IsNullOrWhiteSpace(rawBody)
            ? JsonSerializer.Serialize(body, JsonOptions)
            : rawBody;

        using var doc = JsonDocument.Parse(bodyForAuth);
        var root = doc.RootElement;

        if (!ValidateWebhookAuth(root, bodyForAuth, queryToken))
        {
            result.FailureStage = "auth_invalida";
            result.FailureMessage = "Autenticação do webhook inválida (token/signature).";
            return result;
        }

        if (!IsApprovedWebhook(root))
        {
            var eventType = ReadWebhookEventType(root);
            result.FailureStage = "evento_ignorado";
            result.FailureMessage = string.IsNullOrWhiteSpace(eventType)
                ? "Evento do webhook não exige liberação de crédito."
                : $"Evento {eventType} não exige liberação de crédito.";
            result.FailureDetails = eventType;
            return result;
        }

        var orderId = ExtractOrderId(root);
        if (string.IsNullOrWhiteSpace(orderId))
        {
            result.FailureStage = "order_id_ausente";
            result.FailureMessage = "Webhook de compra aprovada sem order_id/order_ref.";
            return result;
        }

        try
        {
            var verify = await VerifyPaymentAsync(orderId, cancellationToken, root);
            result.Verification = verify;

            if (!verify.Paid)
            {
                result.FailureStage = "pagamento_nao_aprovado";
                result.FailureMessage = string.IsNullOrWhiteSpace(verify.PaymentStatus)
                    ? "Pagamento não está aprovado na verificação."
                    : $"Pagamento com status {verify.PaymentStatus}.";
                result.FailureDetails = verify.PaymentStatus;
                return result;
            }

            if (verify.Paid)
            {
                _logger.LogInformation(
                    "Webhook Kiwify processado: order={OrderId} user={UserId} credits={Credits} alreadyFulfilled={AlreadyFulfilled}",
                    orderId,
                    verify.User?.Id,
                    verify.User?.Credits,
                    verify.AlreadyFulfilled);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar webhook Kiwify order={OrderId}", orderId);
            result.FailureStage = "processamento_falhou";
            result.FailureMessage = ex.Message;
            result.FailureDetails = ex.GetType().Name;
            return result;
        }
    }

    public async Task<KiwifySaleDetailsDto> GetSaleDetailsAsync(
        string orderId,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var sale = await GetSaleAsync(orderId, cancellationToken);
        var status = ReadString(sale, "order_status", "status");
        var orderRef = ReadString(sale, "order_ref", "reference");
        var orderUuid = ReadString(sale, "order_id", "id", "sale_id");
        var paymentId = !string.IsNullOrWhiteSpace(orderRef) ? orderRef.Trim() : orderId.Trim();
        var externalRef = ResolveExternalReference(sale, null);
        var existing = await _purchases.GetPurchaseByPaymentIdAsync(paymentId, cancellationToken);
        if (existing == null && !string.IsNullOrWhiteSpace(orderUuid) && orderUuid != paymentId)
        {
            existing = await _purchases.GetPurchaseByPaymentIdAsync(orderUuid, cancellationToken);
        }

        return new KiwifySaleDetailsDto
        {
            OrderId = orderUuid ?? orderId,
            OrderRef = orderRef,
            Status = status,
            Paid = IsPaidStatus(status),
            AlreadyFulfilled = existing is { Status: "concluida" or "completed" },
            CustomerEmail = ReadCustomerEmail(sale),
            PriceBRL = ReadSalePriceBrl(sale, 0),
            ExternalReference = externalRef,
            PaymentIdUsed = paymentId
        };
    }

    public Task<PaymentVerificationResult> ReconcileOrderAsync(
        string orderId,
        CancellationToken cancellationToken = default) =>
        VerifyPaymentAsync(orderId, cancellationToken);

    public async Task<KiwifyAutoReconcileResult> ReconcileRecentSalesAsync(
        int lookbackMinutes = 1440,
        int pageSize = 100,
        int maxPages = 5,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        lookbackMinutes = Math.Clamp(lookbackMinutes, 1, 60 * 24 * 30);
        pageSize = Math.Clamp(pageSize, 1, 100);
        maxPages = Math.Clamp(maxPages, 1, 10);

        var summary = new KiwifyAutoReconcileResult();
        var candidates = await ListRecentPaidSalesAsync(lookbackMinutes, pageSize, maxPages, cancellationToken);
        summary.Candidates = candidates.Count;

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            summary.Checked += 1;

            try
            {
                var details = await GetSaleDetailsAsync(candidate.OrderId, cancellationToken);
                if (!details.Paid)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(details.ExternalReference))
                {
                    summary.SkippedWithoutReference += 1;
                    continue;
                }

                if (details.AlreadyFulfilled)
                {
                    summary.AlreadyFulfilled += 1;
                    continue;
                }

                var result = await ReconcileOrderAsync(candidate.OrderId, cancellationToken);
                if (!result.Paid)
                {
                    continue;
                }

                if (result.AlreadyFulfilled)
                {
                    summary.AlreadyFulfilled += 1;
                    continue;
                }

                summary.Processed += 1;

                var meta = ParseExternalReference(details.ExternalReference);
                if (!string.IsNullOrWhiteSpace(result.User?.Id) && !string.IsNullOrWhiteSpace(meta?.P))
                {
                    await _purchases.MarkPendingPurchasesSubstitutedAsync(
                        result.User!.Id,
                        meta!.P!,
                        cancellationToken);
                }
            }
            catch (Exception ex)
            {
                summary.Errors += 1;
                _logger.LogWarning(ex, "Kiwify auto reconcile falhou para venda {OrderId}", candidate.OrderId);
            }
        }

        return summary;
    }

    private async Task RegisterPendingPurchaseSafeAsync(
        CheckoutContext ctx,
        CancellationToken cancellationToken)
    {
        try
        {
            await _purchases.CreatePendingPurchaseAsync(
                ctx.UserId,
                ctx.PlanId,
                ctx.PlanName,
                ctx.Analyses,
                ctx.AmountBRL,
                paymentMethod: "kiwify",
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao registrar compra pendente Kiwify user={UserId}", ctx.UserId);
        }
    }

    public async Task<PaymentProviderTestResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var webhookUrl = $"{GetApiBaseUrl()}/api/analyze/payment/kiwify/webhook";

        if (!KiwifyConfigHelper.HasApiCredentials(_configuration))
        {
            return new PaymentProviderTestResult
            {
                Connected = false,
                Provider = "kiwify",
                Message = "Defina KIWIFY_API_KEY, KIWIFY_CLIENT_SECRET e KIWIFY_ACCOUNT_ID no backend/.env"
            };
        }

        try
        {
            await EnsureAccessTokenAsync(cancellationToken);

            var checkoutCodes = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["single"] = KiwifyConfigHelper.GetCheckoutCode(_configuration, "single", false),
                ["pack3"] = KiwifyConfigHelper.GetCheckoutCode(_configuration, "pack3", false),
                ["pack5"] = KiwifyConfigHelper.GetCheckoutCode(_configuration, "pack5", false),
                ["english"] = KiwifyConfigHelper.GetCheckoutCode(_configuration, "english", false)
            };

            var configuredCount = checkoutCodes.Count(kv => !string.IsNullOrWhiteSpace(kv.Value));
            var message = configuredCount > 0
                ? $"Conexão com Kiwify OK (OAuth válido). {configuredCount} link(s) de checkout configurado(s)."
                : "Conexão com Kiwify OK (OAuth válido). Configure KIWIFY_CHECKOUT_SINGLE, PACK3, PACK5 e ENGLISH no .env.";

            return new PaymentProviderTestResult
            {
                Connected = true,
                Provider = "kiwify",
                Message = message,
                Details = new
                {
                    webhookUrl,
                    oauthOk = true,
                    accountIdPreview = KiwifyConfigHelper.MaskSecret(KiwifyConfigHelper.GetAccountId(_configuration)),
                    apiKeyPreview = KiwifyConfigHelper.MaskSecret(KiwifyConfigHelper.GetApiKey(_configuration)),
                    webhookTokenConfigured = !string.IsNullOrWhiteSpace(KiwifyConfigHelper.GetWebhookToken(_configuration)),
                    checkoutCodes = checkoutCodes.ToDictionary(
                        kv => kv.Key,
                        kv => string.IsNullOrWhiteSpace(kv.Value) ? "(não definido)" : kv.Value)
                }
            };
        }
        catch (Exception ex)
        {
            return new PaymentProviderTestResult
            {
                Connected = false,
                Provider = "kiwify",
                Message = ex.Message,
                Details = new { webhookUrl }
            };
        }
    }

    private async Task<JsonElement> GetSaleAsync(string orderId, CancellationToken cancellationToken)
    {
        var client = await CreateAuthorizedClientAsync(cancellationToken);
        using var response = await client.GetAsync($"v1/sales/{Uri.EscapeDataString(orderId)}", cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Erro ao buscar venda Kiwify: {json}");
        }

        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private async Task<List<KiwifyRecentSaleCandidate>> ListRecentPaidSalesAsync(
        int lookbackMinutes,
        int pageSize,
        int maxPages,
        CancellationToken cancellationToken)
    {
        var from = DateTimeOffset.UtcNow.AddMinutes(-lookbackMinutes);
        var to = DateTimeOffset.UtcNow;
        var startDate = from.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var endDate = to.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var client = await CreateAuthorizedClientAsync(cancellationToken);
        var result = new List<KiwifyRecentSaleCandidate>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var page = 1; page <= maxPages; page++)
        {
            var url =
                $"v1/sales?start_date={Uri.EscapeDataString(startDate)}" +
                $"&end_date={Uri.EscapeDataString(endDate)}" +
                $"&status=paid&view_full_sale_details=true" +
                $"&page_size={pageSize}&page_number={page}";

            using var response = await client.GetAsync(url, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Erro ao listar vendas Kiwify: {json}");
            }

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            {
                break;
            }

            var itemsOnPage = 0;
            foreach (var sale in data.EnumerateArray())
            {
                itemsOnPage += 1;

                var status = ReadString(sale, "status", "order_status");
                if (!IsPaidStatus(status))
                {
                    continue;
                }

                var orderId = ReadString(sale, "id", "order_id", "sale_id");
                if (string.IsNullOrWhiteSpace(orderId))
                {
                    continue;
                }

                orderId = orderId.Trim();
                if (!seen.Add(orderId))
                {
                    continue;
                }

                var activityAt = ReadDateTimeOffset(sale, "updated_at", "approved_date", "created_at");
                if (activityAt.HasValue && activityAt.Value < from)
                {
                    continue;
                }

                var externalReference = ExtractExternalReference(sale);
                if (string.IsNullOrWhiteSpace(externalReference))
                {
                    continue;
                }

                result.Add(new KiwifyRecentSaleCandidate
                {
                    OrderId = orderId
                });
            }

            if (itemsOnPage < pageSize)
            {
                break;
            }
        }

        return result;
    }

    private static bool IsApprovedWebhook(JsonElement root)
    {
        var eventType = ReadWebhookEventType(root);

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            var normalized = eventType.Trim().ToLowerInvariant();
            if (normalized is "compra_aprovada" or "order_approved" or "purchase_approved")
            {
                return true;
            }

            if (normalized is "compra_recusada" or "order_refused" or "pix_gerado")
            {
                return false;
            }

            if (normalized.Contains('.', StringComparison.Ordinal))
            {
                return false;
            }
        }

        var body = GetWebhookBody(root);
        var status = ReadString(body, "order_status", "status");
        return IsPaidStatus(status);
    }

    private static string? ReadWebhookEventType(JsonElement root)
    {
        var body = GetWebhookBody(root);
        return ReadString(body, "webhook_event_type", "event", "type")
            ?? ReadString(root, "webhook_event_type", "event", "type");
    }

    private static bool IsPaidStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return false;
        }

        var normalized = status.Trim().ToLowerInvariant();
        return normalized is "paid" or "approved" or "compra_aprovada" or "pago";
    }

    private static string? ExtractOrderId(JsonElement root)
    {
        var body = GetWebhookBody(root);

        var orderId = ReadString(body, "order_id", "sale_id");
        if (!string.IsNullOrWhiteSpace(orderId))
        {
            return orderId.Trim();
        }

        var orderRef = ReadString(body, "order_ref", "reference");
        if (!string.IsNullOrWhiteSpace(orderRef))
        {
            return orderRef.Trim();
        }

        if (!IsWebhookEnvelope(root) && !HasKiwifyOrderWrapper(root))
        {
            var id = ReadString(root, "id");
            if (!string.IsNullOrWhiteSpace(id) && !LooksLikeEventUuid(id))
            {
                return id.Trim();
            }
        }

        return null;
    }

    private static string ResolvePaymentId(JsonElement sale, JsonElement webhookOrder, string orderId)
    {
        if (webhookOrder.ValueKind == JsonValueKind.Object)
        {
            var orderRef = ReadString(webhookOrder, "order_ref");
            if (!string.IsNullOrWhiteSpace(orderRef))
            {
                return orderRef.Trim();
            }
        }

        var refFromSale = ReadString(sale, "order_ref", "reference");
        if (!string.IsNullOrWhiteSpace(refFromSale))
        {
            return refFromSale.Trim();
        }

        return orderId.Trim();
    }

    private bool ValidateWebhookAuth(JsonElement root, string rawBody, string? queryToken = null)
    {
        var configuredToken = KiwifyConfigHelper.GetWebhookToken(_configuration);
        if (string.IsNullOrWhiteSpace(configuredToken))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(queryToken) &&
            string.Equals(queryToken.Trim(), configuredToken, StringComparison.Ordinal))
        {
            return true;
        }

        var tokenInPayload = ReadString(root, "token");
        if (!string.IsNullOrWhiteSpace(tokenInPayload))
        {
            return string.Equals(tokenInPayload, configuredToken, StringComparison.Ordinal);
        }

        var signature = ReadString(root, "signature");
        if (!string.IsNullOrWhiteSpace(signature))
        {
            var expected = ComputeHmacSha1Hex(rawBody, configuredToken);
            if (string.Equals(signature, expected, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Kiwify Apps envia "signature" (hash); o token do painel pode não ser HMAC do body.
            _logger.LogDebug(
                "Webhook Kiwify: signature presente mas HMAC-SHA1(body, token) não confere — aceitando entrega");
            return true;
        }

        // Apps → Webhooks (vendas): corpo é só o objeto order, sem token/signature no JSON.
        // A confirmação real ocorre em VerifyPaymentAsync via API Kiwify + sck do checkout.
        if (LooksLikeFlatOrderPayload(root))
        {
            _logger.LogInformation(
                "Webhook Kiwify: payload order flat (sem token/signature) — aceito; confirmação via API Kiwify.");
            return true;
        }

        _logger.LogWarning(
            "Webhook Kiwify sem campo token/signature no payload; defina KIWIFY_WEBHOOK_TOKEN vazio para aceitar sem auth");
        return false;
    }

    private static bool LooksLikeFlatOrderPayload(JsonElement root) =>
        !string.IsNullOrWhiteSpace(TryReadString(root, "order_id", "sale_id", "order_ref", "reference")) ||
        !string.IsNullOrWhiteSpace(TryReadString(root, "order_status", "status")) ||
        !string.IsNullOrWhiteSpace(TryReadString(root, "webhook_event_type", "event", "type"));

    private static string? TryReadString(JsonElement source, params string[] names)
    {
        foreach (var name in names)
        {
            if (!source.TryGetProperty(name, out var prop))
            {
                continue;
            }

            var value = prop.ValueKind switch
            {
                JsonValueKind.String => prop.GetString(),
                JsonValueKind.Number => prop.GetRawText(),
                _ => null
            };

            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string ComputeHmacSha1Hex(string payload, string secret)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        var data = Encoding.UTF8.GetBytes(payload);
        using var hmac = new HMACSHA1(key);
        return Convert.ToHexString(hmac.ComputeHash(data)).ToLowerInvariant();
    }

    private static bool HasKiwifyOrderWrapper(JsonElement root) =>
        root.TryGetProperty("order", out var order) && order.ValueKind == JsonValueKind.Object;

    private static bool IsWebhookEnvelope(JsonElement root) =>
        root.TryGetProperty("data", out var data)
        && data.ValueKind == JsonValueKind.Object
        && (root.TryGetProperty("version", out _) || IsBankingDeliveryType(ReadString(root, "type")));

    private static bool IsBankingDeliveryType(string? type) =>
        !string.IsNullOrWhiteSpace(type) && type.Contains('.', StringComparison.Ordinal);

    /// <summary>Corpo do evento: objeto <c>order</c> (webhook de vendas), envelope banking <c>data</c>, ou raiz.</summary>
    private static JsonElement GetWebhookBody(JsonElement root)
    {
        if (HasKiwifyOrderWrapper(root))
        {
            return root.GetProperty("order");
        }

        if (IsWebhookEnvelope(root) && root.TryGetProperty("data", out var data))
        {
            return data;
        }

        return root;
    }

    private static bool LooksLikeEventUuid(string value) => Guid.TryParse(value, out _);

    private static string? ResolveExternalReference(JsonElement sale, JsonElement? webhookPayload)
    {
        var fromSale = ExtractExternalReference(sale);
        if (!string.IsNullOrWhiteSpace(fromSale))
        {
            return fromSale;
        }

        if (!webhookPayload.HasValue)
        {
            return null;
        }

        var webhook = webhookPayload.Value;
        return ExtractExternalReference(webhook)
            ?? ExtractExternalReference(GetWebhookBody(webhook));
    }

    private static string? ExtractExternalReference(JsonElement source)
    {
        foreach (var container in EnumerateTrackingContainers(source))
        {
            var sck = ReadString(container, "sck");
            if (!string.IsNullOrWhiteSpace(sck))
            {
                return NormalizeSck(sck);
            }
        }

        var direct = ReadString(source, "sck");
        return !string.IsNullOrWhiteSpace(direct) ? NormalizeSck(direct) : null;
    }

    private static IEnumerable<JsonElement> EnumerateTrackingContainers(JsonElement source)
    {
        foreach (var name in new[] { "tracking", "TrackingParameters", "Tracking" })
        {
            if (source.TryGetProperty(name, out var tracking) && tracking.ValueKind == JsonValueKind.Object)
            {
                yield return tracking;
            }
        }
    }

    private static string NormalizeSck(string value)
    {
        var trimmed = value.Trim();
        try
        {
            return Uri.UnescapeDataString(trimmed);
        }
        catch (UriFormatException)
        {
            return trimmed;
        }
    }

    private static KiwifyExternalReference? ParseExternalReference(string? externalReference)
    {
        if (string.IsNullOrWhiteSpace(externalReference))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<KiwifyExternalReference>(externalReference, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static FulfillOrderRequest BuildFulfillRequest(
        KiwifyExternalReference meta,
        string paymentId,
        JsonElement sale,
        JsonElement? webhookPayload = null)
    {
        decimal price = meta.M;
        price = ReadSalePriceBrl(sale, price);

        var email = ReadCustomerEmail(sale);
        if (string.IsNullOrWhiteSpace(email) && webhookPayload.HasValue)
        {
            email = ReadCustomerEmail(GetWebhookBody(webhookPayload.Value));
        }

        return new FulfillOrderRequest
        {
            UserId = meta.U ?? string.Empty,
            PlanId = meta.P ?? string.Empty,
            PlanName = meta.N ?? $"Plano {meta.P}",
            Analyses = meta.A,
            Price = price,
            PaymentMethod = "kiwify",
            PaymentId = paymentId,
            CustomerEmail = email,
            CouponId = meta.C,
            DiscountPercent = meta.D,
            OriginalPrice = meta.O,
            CpfNormalized = meta.F,
            IncludeEnglish = meta.E,
            EnglishPriceBRL = meta.G ?? 0,
            AnalysisId = meta.I,
            SendConfirmationEmail = true
        };
    }

    private static decimal ReadSalePriceBrl(JsonElement sale, decimal fallback)
    {
        if (TryGetProperty(sale, "Commissions", "commissions", out var commissions))
        {
            var fromCommission = ReadMoneyBRL(commissions, "charge_amount", fallback);
            if (fromCommission != fallback)
            {
                return fromCommission;
            }

            fromCommission = ReadMoneyBRL(commissions, "product_base_price", fallback);
            if (fromCommission != fallback)
            {
                return fromCommission;
            }
        }

        if (TryGetProperty(sale, "payment", "Payment", out var payment))
        {
            var fromPayment = ReadMoneyBRL(payment, "charge_amount", fallback);
            if (fromPayment != fallback)
            {
                return fromPayment;
            }

            return ReadMoneyBRL(payment, "product_base_price", fallback);
        }

        return fallback;
    }

    private static string ReadCustomerEmail(JsonElement source)
    {
        if (!TryGetProperty(source, "customer", "Customer", out var customer))
        {
            return string.Empty;
        }

        return ReadString(customer, "email", "Email") ?? string.Empty;
    }

    private static bool TryGetProperty(
        JsonElement element,
        string name1,
        string name2,
        out JsonElement value)
    {
        if (element.TryGetProperty(name1, out value))
        {
            return true;
        }

        if (element.TryGetProperty(name2, out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    private static decimal ReadMoneyBRL(JsonElement parent, string property, decimal fallback)
    {
        if (!parent.TryGetProperty(property, out var valueProp))
        {
            return fallback;
        }

        if (valueProp.ValueKind == JsonValueKind.Number && valueProp.TryGetDecimal(out var number))
        {
            return number >= 100m ? number / 100m : number;
        }

        if (valueProp.ValueKind == JsonValueKind.String
            && decimal.TryParse(valueProp.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed >= 100m ? parsed / 100m : parsed;
        }

        return fallback;
    }

    private static string? ReadString(JsonElement element, params string[] propertyNames)
    {
        foreach (var property in propertyNames)
        {
            if (!element.TryGetProperty(property, out var prop))
            {
                continue;
            }

            var value = ReadStringValue(prop);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string? ReadStringValue(JsonElement prop) =>
        prop.ValueKind switch
        {
            JsonValueKind.String => prop.GetString(),
            JsonValueKind.Number => prop.GetRawText(),
            _ => null
        };

    private static DateTimeOffset? ReadDateTimeOffset(JsonElement source, params string[] names)
    {
        foreach (var name in names)
        {
            if (!source.TryGetProperty(name, out var prop))
            {
                continue;
            }

            var raw = ReadStringValue(prop);
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            if (DateTimeOffset.TryParse(
                    raw,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces,
                    out var parsed))
            {
                return parsed;
            }

            if (DateTime.TryParse(
                    raw,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal | DateTimeStyles.AllowWhiteSpaces,
                    out var localParsed))
            {
                return new DateTimeOffset(localParsed);
            }
        }

        return null;
    }

    private static string BuildExternalReference(CheckoutContext ctx)
    {
        var payload = new KiwifyExternalReference
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

    private static string BuildHostedCheckoutUrl(
        string checkoutCode,
        string customerName,
        string email,
        string? cpfNormalized,
        string? couponCode,
        string externalReference)
    {
        var query = new List<string>
        {
            $"name={Uri.EscapeDataString(customerName)}",
            $"email={Uri.EscapeDataString(email)}",
            $"phone={Uri.EscapeDataString(DefaultCustomerPhone)}",
            $"region=br",
            "hideBoleto=true",
            $"sck={Uri.EscapeDataString(externalReference)}"
        };

        if (!string.IsNullOrWhiteSpace(cpfNormalized) && cpfNormalized.Length == 11)
        {
            query.Add($"cpf={Uri.EscapeDataString(cpfNormalized)}");
        }

        if (!string.IsNullOrWhiteSpace(couponCode))
        {
            query.Add($"coupon={Uri.EscapeDataString(couponCode.Trim())}");
        }

        return $"https://pay.kiwify.com.br/{checkoutCode.Trim()}?{string.Join("&", query)}";
    }

    private async Task<HttpClient> CreateAuthorizedClientAsync(CancellationToken cancellationToken)
    {
        await EnsureAccessTokenAsync(cancellationToken);
        return CreateAuthorizedClient();
    }

    private HttpClient CreateAuthorizedClient()
    {
        var client = _httpClientFactory.CreateClient("Kiwify");
        client.BaseAddress = new Uri($"{KiwifyConfigHelper.BaseUrl}/");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        client.DefaultRequestHeaders.Remove("x-kiwify-account-id");
        client.DefaultRequestHeaders.Add("x-kiwify-account-id", KiwifyConfigHelper.GetAccountId(_configuration));
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

            var apiKey = KiwifyConfigHelper.GetApiKey(_configuration)
                ?? throw new InvalidOperationException("KIWIFY_API_KEY não configurado");
            var clientSecret = KiwifyConfigHelper.GetClientSecret(_configuration)
                ?? throw new InvalidOperationException("KIWIFY_CLIENT_SECRET não configurado");
            var accountId = KiwifyConfigHelper.GetAccountId(_configuration)
                ?? throw new InvalidOperationException("KIWIFY_ACCOUNT_ID não configurado");

            var client = _httpClientFactory.CreateClient("Kiwify");
            client.BaseAddress = new Uri($"{KiwifyConfigHelper.BaseUrl}/");
            client.DefaultRequestHeaders.Remove("x-kiwify-account-id");
            client.DefaultRequestHeaders.Add("x-kiwify-account-id", accountId);

            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = apiKey,
                ["client_secret"] = clientSecret
            });
            using var response = await client.PostAsync("v1/oauth/token", content, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Erro ao autenticar na Kiwify: {json}");
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            _accessToken = root.TryGetProperty("access_token", out var at)
                ? at.GetString()
                : root.TryGetProperty("token", out var t) ? t.GetString() : null;

            if (string.IsNullOrWhiteSpace(_accessToken))
            {
                throw new InvalidOperationException("Kiwify não retornou access_token.");
            }

            var expiresIn = root.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 345600;
            _tokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn);
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private void EnsureConfigured()
    {
        if (!KiwifyConfigHelper.IsConfigured(_configuration))
        {
            throw new InvalidOperationException(KiwifyConfigHelper.BuildMissingConfigMessage(_configuration));
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

    private static string TruncatePlanName(string value) =>
        value.Length <= 40 ? value : value[..40];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private sealed class KiwifyExternalReference
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

    private sealed class KiwifyRecentSaleCandidate
    {
        public string OrderId { get; set; } = string.Empty;
    }
}
