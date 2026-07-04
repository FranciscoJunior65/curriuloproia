using CurriculosProIA.Domain.Dtos;
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

    Task<PaymentVerificationResult?> HandleWebhookAsync(HttpRequest request, CancellationToken cancellationToken = default);

    Task<KiwifySaleDetailsDto> GetSaleDetailsAsync(string orderId, CancellationToken cancellationToken = default);

    Task<PaymentVerificationResult> ReconcileOrderAsync(string orderId, CancellationToken cancellationToken = default);

    Task<PaymentProviderTestResult> TestConnectionAsync(CancellationToken cancellationToken = default);
}
