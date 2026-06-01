using Microsoft.AspNetCore.Http;
using CurriculosProIA.Domain.Entities;
using CurriculosProIA.Domain.Dtos;
using CurriculosProIA.Repository.Persistence;
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

    Task<PaymentVerificationResult> VerifyPaymentAsync(string paymentId, CancellationToken cancellationToken = default);

    Task HandleWebhookAsync(IQueryCollection query, CancellationToken cancellationToken = default);

    Task<PaymentProviderTestResult> TestConnectionAsync(CancellationToken cancellationToken = default);
}
