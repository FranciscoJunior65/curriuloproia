using Microsoft.AspNetCore.Http;
using CurriculosProIA.Domain.Dtos;

namespace CurriculosProIA.Service.Interfaces;

public interface IMercadoPagoService
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

    Task<MercadoPagoProcessPaymentResult> ProcessCardPaymentAsync(
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
        CancellationToken cancellationToken = default);

    Task<MercadoPagoPixPaymentResult> CreatePixPaymentAsync(
        string planId,
        string userId,
        string email,
        string? couponCode = null,
        string? cpf = null,
        bool includeEnglish = false,
        string? analysisId = null,
        CancellationToken cancellationToken = default);

    Task<PaymentVerificationResult> VerifyPaymentAsync(string paymentId, CancellationToken cancellationToken = default);

    Task HandleWebhookAsync(HttpRequest request, CancellationToken cancellationToken = default);

    Task<PaymentProviderTestResult> TestConnectionAsync(CancellationToken cancellationToken = default);
}
