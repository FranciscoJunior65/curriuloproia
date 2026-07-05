using CurriculosProIA.Domain.Dtos;
using CurriculosProIA.Domain.Signatures.Analyze;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
namespace CurriculosProIA.Service.Interfaces;

public interface IKiwifyService
{
    Task<CheckoutSessionResult> CreateCheckoutAsync(
        string planId,
        string userId,
        string email,
        string? frontendUrl = null,
        string? couponCode = null,
        string? cpf = null,
        bool includeEnglish = false,
        string? analysisId = null,
        CancellationToken cancellationToken = default);

    Task<PaymentVerificationResult> VerifyPaymentAsync(
        string orderId,
        CancellationToken cancellationToken = default,
        JsonElement? webhookPayload = null);

    Task<KiwifyWebhookHandleResult> HandleWebhookAsync(
        KiwifyWebhookSignature body,
        string? rawBody = null,
        CancellationToken cancellationToken = default,
        string? queryToken = null);

    Task<KiwifySaleDetailsDto> GetSaleDetailsAsync(string orderId, CancellationToken cancellationToken = default);

    Task<PaymentVerificationResult> ReconcileOrderAsync(string orderId, CancellationToken cancellationToken = default);

    Task<KiwifyAutoReconcileResult> ReconcileRecentSalesAsync(
        int lookbackMinutes = 1440,
        int pageSize = 100,
        int maxPages = 5,
        CancellationToken cancellationToken = default);

    Task<PaymentProviderTestResult> TestConnectionAsync(CancellationToken cancellationToken = default);
}
