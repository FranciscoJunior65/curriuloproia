using CurriculosProIA.Domain.Entities;
using CurriculosProIA.Domain.Dtos;
using CurriculosProIA.Repository.Persistence;

namespace CurriculosProIA.Service.Interfaces;

public interface IStripePaymentService
{
    Task<CheckoutSessionResult> CreateCheckoutSessionAsync(
        string planId,
        string userId,
        string email,
        string? frontendUrl = null,
        string? couponCode = null,
        string? cpf = null,
        bool includeEnglish = false,
        string? analysisId = null,
        CancellationToken cancellationToken = default);

    Task<Stripe.Checkout.Session> GetCheckoutSessionAsync(string sessionId, CancellationToken cancellationToken = default);

    Task HandleWebhookAsync(byte[] rawBody, string signature, CancellationToken cancellationToken = default);

    Task<PaymentProviderTestResult> TestConnectionAsync(CancellationToken cancellationToken = default);
}
