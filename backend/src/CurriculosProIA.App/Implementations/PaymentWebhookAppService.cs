using System.Text;
using System.Text.Json;
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

namespace CurriculosProIA.App.Implementations;
using CurriculosProIA.App;
using CurriculosProIA.App.Interfaces;

public class PaymentWebhookAppService : AppControllerBase, IPaymentWebhookAppService 
{
    private const string KiwifyWebhookApiVersion = "kiwify-webhook-v2";

    private readonly IHttpContextAccessor _http;
    private readonly IStripePaymentService _stripe;
    private readonly IMercadoPagoService _mercadoPago;
    private readonly ICaktoService _cakto;
    private readonly IKiwifyService _kiwify;
    private readonly IKiwifyWebhookLogRepository _kiwifyWebhookLogs;
    private readonly ILogger<PaymentWebhookAppService> _logger;

    public PaymentWebhookAppService(
        IStripePaymentService stripe,
        IMercadoPagoService mercadoPago,
        ICaktoService cakto,
        IKiwifyService kiwify,
        IKiwifyWebhookLogRepository kiwifyWebhookLogs,
        IHttpContextAccessor http,
        ILogger<PaymentWebhookAppService> logger)
    {
        _stripe = stripe;
        _mercadoPago = mercadoPago;
        _cakto = cakto;
        _kiwify = kiwify;
        _kiwifyWebhookLogs = kiwifyWebhookLogs;
        _http = http;
        _logger = logger;
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
        var request = _http.HttpContext!.Request;
        var rawBody = await ReadRequestBodyAsync(request, cancellationToken);
        var payload = TryResolveKiwifyPayload(request, rawBody);
        var order = payload?.Order;
        var eventType = order?.WebhookEventType;
        var orderId = order?.OrderId;
        var orderRef = order?.OrderRef;
        var paymentStatus = order?.OrderStatus;
        var parsedPayload = payload == null ? null : SafeSerialize(payload);

        KiwifyWebhookHandleResult? handleResult = null;
        string? error = null;
        var httpStatus = StatusCodes.Status200OK;
        object responseBody;

        if (order == null)
        {
            responseBody = new
            {
                received = true,
                processed = false,
                alreadyFulfilled = false,
                credits = (int?)null,
                userId = (string?)null,
                apiVersion = KiwifyWebhookApiVersion,
                message = "CurriculosProIA recebeu o webhook, mas o payload veio vazio ou invalido.",
                orderId,
                orderRef,
                eventType,
                paymentStatus,
                failureStage = "payload_invalido"
            };
        }
        else
        {
            try
            {
                handleResult = await _kiwify.HandleWebhookAsync(
                    payload!,
                    rawBody,
                    cancellationToken,
                    request.Query["token"].FirstOrDefault());
                var result = handleResult.Verification;
                var processed = result?.Paid == true;
                var alreadyFulfilled = result?.AlreadyFulfilled == true;
                var message = BuildKiwifyWebhookMessage(
                    processed,
                    alreadyFulfilled,
                    eventType,
                    paymentStatus,
                    handleResult);

                responseBody = new
                {
                    received = true,
                    processed,
                    alreadyFulfilled,
                    credits = result?.User?.Credits,
                    userId = result?.User?.Id,
                    apiVersion = KiwifyWebhookApiVersion,
                    message,
                    orderId,
                    orderRef,
                    eventType,
                    paymentStatus,
                    failureStage = handleResult.FailureStage,
                    failureMessage = handleResult.FailureMessage
                };
            }
            catch (Exception ex)
            {
                httpStatus = StatusCodes.Status500InternalServerError;
                error = ex.Message;
                responseBody = new
                {
                    received = false,
                    processed = false,
                    alreadyFulfilled = false,
                    credits = (int?)null,
                    userId = (string?)null,
                    apiVersion = KiwifyWebhookApiVersion,
                    message = $"CurriculosProIA com erro - {ex.Message}",
                    orderId,
                    orderRef,
                    eventType,
                    paymentStatus,
                    failureStage = "exception",
                    failureMessage = ex.Message
                };
            }
        }

        await SaveKiwifyWebhookLogSafeAsync(
            rawBody,
            parsedPayload,
            orderId,
            orderRef,
            eventType,
            paymentStatus,
            responseBody,
            httpStatus,
            handleResult,
            error,
            cancellationToken);

        return httpStatus == StatusCodes.Status200OK
            ? Ok(responseBody)
            : StatusCode(httpStatus, responseBody);
    }

    private async Task SaveKiwifyWebhookLogSafeAsync(
        string rawBody,
        string? parsedPayload,
        string? orderId,
        string? orderRef,
        string? eventType,
        string? paymentStatus,
        object responseBody,
        int httpStatus,
        KiwifyWebhookHandleResult? handleResult,
        string? error,
        CancellationToken cancellationToken)
    {
        try
        {
            var responseJson = SafeSerialize(responseBody);
            var processed = handleResult?.Verification?.Paid == true;
            var alreadyFulfilled = handleResult?.Verification?.AlreadyFulfilled == true;

            await _kiwifyWebhookLogs.CreateAsync(
                new CreateKiwifyWebhookLogRequest
                {
                    PayloadRecebido = rawBody,
                    PayloadParseado = parsedPayload,
                    OrderId = orderId,
                    OrderRef = orderRef,
                    EventType = eventType,
                    PaymentStatus = paymentStatus,
                    Processed = processed,
                    AlreadyFulfilled = alreadyFulfilled,
                    Credits = handleResult?.Verification?.User?.Credits,
                    UserId = handleResult?.Verification?.User?.Id,
                    HttpStatus = httpStatus,
                    ApiVersion = KiwifyWebhookApiVersion,
                    Message = TryReadResponseMessage(responseJson),
                    RespostaJson = responseJson,
                    Erro = error ?? handleResult?.FailureMessage,
                    FailureStage = handleResult?.FailureStage ?? TryReadResponseFailureStage(responseJson),
                    ProcessingDetails = BuildProcessingDetails(handleResult)
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao gravar log do webhook Kiwify order={OrderId}", orderId);
        }
    }

    private static string? BuildProcessingDetails(KiwifyWebhookHandleResult? handleResult)
    {
        if (handleResult == null)
        {
            return null;
        }

        return SafeSerialize(new
        {
            handleResult.FailureStage,
            handleResult.FailureMessage,
            handleResult.FailureDetails,
            verification = handleResult.Verification == null
                ? null
                : new
                {
                    handleResult.Verification.Paid,
                    handleResult.Verification.PaymentStatus,
                    handleResult.Verification.AlreadyFulfilled,
                    userId = handleResult.Verification.User?.Id,
                    credits = handleResult.Verification.User?.Credits
                }
        });
    }

    private static string SafeSerialize(object? value)
    {
        if (value == null)
        {
            return "{}";
        }

        try
        {
            return JsonSerializer.Serialize(value, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
        }
        catch
        {
            return "{}";
        }
    }

    private static string? TryReadResponseMessage(string responseJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            return doc.RootElement.TryGetProperty("message", out var message)
                ? message.GetString()
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? TryReadResponseFailureStage(string responseJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            return doc.RootElement.TryGetProperty("failureStage", out var stage)
                ? stage.GetString()
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<string> ReadRequestBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        request.EnableBuffering();
        request.Body.Position = 0;

        using var reader = new StreamReader(
            request.Body,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            leaveOpen: true);

        var rawBody = await reader.ReadToEndAsync(cancellationToken);
        request.Body.Position = 0;

        return rawBody;
    }

    private static KiwifyWebhookSignature? TryResolveKiwifyPayload(HttpRequest request, string rawBody)
    {
        if (TryResolveJsonPayload(rawBody, out var jsonPayload))
        {
            return jsonPayload;
        }

        if (TryResolveFormPayload(request, rawBody, out var formPayload))
        {
            return formPayload;
        }

        return null;
    }

    private static bool TryResolveJsonPayload(string rawBody, out KiwifyWebhookSignature? payload)
    {
        payload = null;

        if (string.IsNullOrWhiteSpace(rawBody))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (TryGetObjectProperty(root, "order", out var orderElement))
            {
                payload = BuildPayload(root, orderElement);
                return payload?.Order != null;
            }

            if (TryGetObjectProperty(root, "data", out var dataElement))
            {
                payload = BuildPayload(root, dataElement);
                return payload?.Order != null;
            }

            if (LooksLikeFlatOrderPayload(root))
            {
                payload = BuildPayload(root, root);
                return payload?.Order != null;
            }

            var direct = JsonSerializer.Deserialize<KiwifyWebhookSignature>(
                rawBody,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (direct?.Order != null)
            {
                payload = direct;
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryResolveFormPayload(HttpRequest request, string rawBody, out KiwifyWebhookSignature? payload)
    {
        payload = null;

        if (request.HasFormContentType)
        {
            var form = request.Form;
            if (form.Count == 0)
            {
                return false;
            }

            var order = new KiwifyWebhookOrderSignature
            {
                OrderId = GetFormValue(form, "order_id", "sale_id", "id"),
                OrderRef = GetFormValue(form, "order_ref", "reference"),
                OrderStatus = GetFormValue(form, "order_status", "status"),
                PaymentMethod = GetFormValue(form, "payment_method"),
                WebhookEventType = GetFormValue(form, "webhook_event_type", "event", "type")
            };

            if (string.IsNullOrWhiteSpace(order.OrderId) &&
                string.IsNullOrWhiteSpace(order.OrderRef) &&
                string.IsNullOrWhiteSpace(order.OrderStatus) &&
                string.IsNullOrWhiteSpace(order.WebhookEventType))
            {
                return false;
            }

            payload = new KiwifyWebhookSignature
            {
                Url = GetFormValue(form, "url"),
                Signature = GetFormValue(form, "signature"),
                Token = GetFormValue(form, "token"),
                Order = order
            };

            return true;
        }

        if (string.IsNullOrWhiteSpace(rawBody) || !rawBody.Contains('='))
        {
            return false;
        }

        var parts = rawBody.Split('&', StringSplitOptions.RemoveEmptyEntries);
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in parts)
        {
            var tokens = part.Split('=', 2);
            var key = Uri.UnescapeDataString(tokens[0]).Trim();
            var value = tokens.Length > 1 ? Uri.UnescapeDataString(tokens[1]).Trim() : string.Empty;
            if (!string.IsNullOrWhiteSpace(key))
            {
                values[key] = value;
            }
        }

        if (values.Count == 0)
        {
            return false;
        }

        var flatOrder = new KiwifyWebhookOrderSignature
        {
            OrderId = GetDictionaryValue(values, "order_id", "sale_id", "id"),
            OrderRef = GetDictionaryValue(values, "order_ref", "reference"),
            OrderStatus = GetDictionaryValue(values, "order_status", "status"),
            PaymentMethod = GetDictionaryValue(values, "payment_method"),
            WebhookEventType = GetDictionaryValue(values, "webhook_event_type", "event", "type")
        };

        if (string.IsNullOrWhiteSpace(flatOrder.OrderId) &&
            string.IsNullOrWhiteSpace(flatOrder.OrderRef) &&
            string.IsNullOrWhiteSpace(flatOrder.OrderStatus) &&
            string.IsNullOrWhiteSpace(flatOrder.WebhookEventType))
        {
            return false;
        }

        payload = new KiwifyWebhookSignature
        {
            Url = GetDictionaryValue(values, "url"),
            Signature = GetDictionaryValue(values, "signature"),
            Token = GetDictionaryValue(values, "token"),
            Order = flatOrder
        };

        return true;
    }

    private static KiwifyWebhookSignature? BuildPayload(JsonElement envelope, JsonElement orderElement)
    {
        try
        {
            var order = JsonSerializer.Deserialize<KiwifyWebhookOrderSignature>(
                orderElement.GetRawText(),
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (order == null)
            {
                return null;
            }

            return new KiwifyWebhookSignature
            {
                Url = TryReadString(envelope, "url"),
                Signature = TryReadString(envelope, "signature"),
                Token = TryReadString(envelope, "token"),
                Order = order
            };
        }
        catch
        {
            return null;
        }
    }

    private static bool TryGetObjectProperty(JsonElement source, string propertyName, out JsonElement value)
    {
        if (source.TryGetProperty(propertyName, out value) && value.ValueKind == JsonValueKind.Object)
        {
            return true;
        }

        value = default;
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

    private static string? GetFormValue(IFormCollection form, params string[] names)
    {
        foreach (var name in names)
        {
            if (form.TryGetValue(name, out var value))
            {
                var item = value.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(item))
                {
                    return item;
                }
            }
        }

        return null;
    }

    private static string? GetDictionaryValue(IReadOnlyDictionary<string, string> values, params string[] names)
    {
        foreach (var name in names)
        {
            if (values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static string BuildKiwifyWebhookMessage(
        bool processed,
        bool alreadyFulfilled,
        string? eventType,
        string? paymentStatus,
        KiwifyWebhookHandleResult? handle = null)
    {
        if (processed && alreadyFulfilled)
        {
            return "CurriculosProIA ja tinha processado esse pagamento.";
        }

        if (processed)
        {
            return "Pagamento no CurriculosProIA com sucesso.";
        }

        if (!string.IsNullOrWhiteSpace(handle?.FailureMessage))
        {
            return $"CurriculosProIA com erro - {handle.FailureMessage}";
        }

        if (string.Equals(paymentStatus, "paid", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(eventType, "order_approved", StringComparison.OrdinalIgnoreCase))
        {
            return "CurriculosProIA recebeu o pagamento aprovado, mas nao conseguiu processar automaticamente.";
        }

        if (!string.IsNullOrWhiteSpace(paymentStatus))
        {
            return $"CurriculosProIA recebeu o webhook, mas o pagamento esta com status {paymentStatus}.";
        }

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            return $"CurriculosProIA recebeu o webhook, mas o evento {eventType} nao exige liberacao de credito.";
        }

        return "CurriculosProIA recebeu o webhook, mas nao conseguiu identificar o evento.";
    }
}
