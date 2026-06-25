using Microsoft.AspNetCore.Http;
using CurriculosProIA.Domain.Dtos;

namespace CurriculosProIA.Service.Interfaces;

public interface ICaktoService
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

    Task<string> CreateCardTokenAsync(
        string holderName,
        string cardNumber,
        string expMonth,
        string expYear,
        string cvv,
        CancellationToken cancellationToken = default);

    /// <summary>Proxy do token 3DS (Cielo/Braspag) — evita CORS do browser para api.cakto.com.br.</summary>
    Task<string> GetThreeDsTokenAsync(string provider = "cielo", CancellationToken cancellationToken = default);

    Task<CaktoProcessPaymentResult> ProcessCardPaymentAsync(
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
        CancellationToken cancellationToken = default);

    Task<CaktoPixPaymentResult> CreatePixPaymentAsync(
        string planId,
        string userId,
        string email,
        string customerName,
        string? couponCode = null,
        string? cpf = null,
        bool includeEnglish = false,
        string? analysisId = null,
        CancellationToken cancellationToken = default);

    Task<PaymentVerificationResult> VerifyPaymentAsync(string orderId, CancellationToken cancellationToken = default);

    Task HandleWebhookAsync(HttpRequest request, CancellationToken cancellationToken = default);

    Task<PaymentProviderTestResult> TestConnectionAsync(CancellationToken cancellationToken = default);
}

public class CaktoThreeDSecureData
{
    public string? Cavv { get; set; }
    public string? Eci { get; set; }
    public string? Xid { get; set; }
    public string? ReferenceId { get; set; }
    public string? Version { get; set; }
    public string? TransStatus { get; set; }
    public string? TdsServerTransId { get; set; }
}
