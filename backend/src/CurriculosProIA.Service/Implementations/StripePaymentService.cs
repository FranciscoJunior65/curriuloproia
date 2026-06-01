using CurriculosProIA.Domain.Entities;
using CurriculosProIA.Domain.Dtos;
using CurriculosProIA.Repository.Persistence;
using CurriculosProIA.Domain.Dtos;
using Stripe;
using Stripe.Checkout;

using CurriculosProIA.Service.Helpers;
using CurriculosProIA.Service.Interfaces;
using CurriculosProIA.Repository.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CurriculosProIA.Service.Implementations;

public class StripePaymentService : IStripePaymentService
{
    private readonly IPaymentCheckoutService _checkout;
    private readonly IPaymentFulfillmentService _fulfillment;
    private readonly IConfiguration _configuration;
    private readonly ILogger<StripePaymentService> _logger;

    public StripePaymentService(
        IPaymentCheckoutService checkout,
        IPaymentFulfillmentService fulfillment,
        IConfiguration configuration,
        ILogger<StripePaymentService> logger)
    {
        _checkout = checkout;
        _fulfillment = fulfillment;
        _configuration = configuration;
        _logger = logger;
        StripeConfiguration.ApiKey = _configuration["STRIPE_SECRET_KEY"];
    }

    public async Task<CheckoutSessionResult> CreateCheckoutSessionAsync(
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

        var statementDescriptor = (_configuration["STRIPE_STATEMENT_DESCRIPTOR"] ?? "CurriculosPro IA")[..Math.Min(22, (_configuration["STRIPE_STATEMENT_DESCRIPTOR"] ?? "CurriculosPro IA").Length)];
        var baseUrl = (frontendUrl ?? _configuration["FRONTEND_URL"] ?? "http://localhost:4200").TrimEnd('/');
        var analysisIdMeta = ctx.Metadata.GetValueOrDefault("analysisId");
        var successUrl = PaymentReturnUrls.Build(
            baseUrl,
            PaymentReturnUrls.SuccessPath,
            "stripe",
            userId,
            analysisIdMeta,
            englishPaid: planId == "english");
        successUrl = successUrl.Replace(
            "?",
            $"?session_id={{CHECKOUT_SESSION_ID}}&",
            StringComparison.Ordinal);

        var options = new SessionCreateOptions
        {
            PaymentMethodTypes = ["card"],
            Mode = "payment",
            SuccessUrl = successUrl,
            CancelUrl = PaymentReturnUrls.Build(
                baseUrl, PaymentReturnUrls.CancelledPath, "stripe", userId, analysisIdMeta),
            Metadata = ctx.Metadata,
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = "brl",
                        UnitAmount = ctx.AmountInCents,
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = ctx.PlanName,
                            Description = ctx.Plan.Description + (ctx.CouponInfo != null
                                ? $" ({ctx.CouponInfo.CouponName}: {ctx.CouponInfo.DiscountPercent}% off)"
                                : string.Empty)
                        }
                    }
                }
            ],
            PaymentIntentData = new SessionPaymentIntentDataOptions
            {
                StatementDescriptor = statementDescriptor
            }
        };

        if (IsValidEmail(email))
        {
            options.CustomerEmail = email.Trim();
        }

        var service = new SessionService();
        var session = await service.CreateAsync(options, cancellationToken: cancellationToken);

        return new CheckoutSessionResult
        {
            SessionId = session.Id,
            Url = session.Url
        };
    }

    public async Task<Session> GetCheckoutSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var service = new SessionService();
        return await service.GetAsync(sessionId, cancellationToken: cancellationToken);
    }

    public async Task HandleWebhookAsync(byte[] rawBody, string signature, CancellationToken cancellationToken = default)
    {
        var webhookSecret = _configuration["STRIPE_WEBHOOK_SECRET"];
        if (string.IsNullOrEmpty(webhookSecret))
        {
            throw new InvalidOperationException("STRIPE_WEBHOOK_SECRET não configurado");
        }

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(
                System.Text.Encoding.UTF8.GetString(rawBody),
                signature,
                webhookSecret);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Webhook Error: {ex.Message}", ex);
        }

        if (stripeEvent.Type == EventTypes.CheckoutSessionCompleted &&
            stripeEvent.Data.Object is Session session &&
            session.PaymentStatus == "paid" &&
            session.Metadata.TryGetValue("userId", out var userId))
        {
            try
            {
                session.Metadata.TryGetValue("planId", out var planId);
                session.Metadata.TryGetValue("planName", out var planName);
                session.Metadata.TryGetValue("analyses", out var analysesStr);
                session.Metadata.TryGetValue("couponId", out var couponId);
                session.Metadata.TryGetValue("couponName", out var couponName);
                session.Metadata.TryGetValue("discountPercent", out var discountPercentStr);
                session.Metadata.TryGetValue("originalPrice", out var originalPriceStr);
                session.Metadata.TryGetValue("cpfNormalized", out var cpfNormalized);
                session.Metadata.TryGetValue("includeEnglish", out var includeEnglishStr);
                session.Metadata.TryGetValue("englishPriceBRL", out var englishPriceStr);
                session.Metadata.TryGetValue("analysisId", out var analysisId);

                decimal? discountPercent = decimal.TryParse(discountPercentStr, out var dp) ? dp : null;
                decimal? originalPrice = decimal.TryParse(originalPriceStr, out var op) ? op : null;
                var includeEnglish = string.Equals(includeEnglishStr, "true", StringComparison.OrdinalIgnoreCase);
                decimal englishPrice = decimal.TryParse(englishPriceStr, out var ep) ? ep : 0;

                await _fulfillment.FulfillPaidOrderAsync(new FulfillOrderRequest
                {
                    UserId = userId ?? string.Empty,
                    PlanId = planId ?? string.Empty,
                    PlanName = planName ?? $"Plano {planId}",
                    Analyses = int.TryParse(analysesStr, out var analyses) ? analyses : 0,
                    Price = (session.AmountTotal ?? 0) / 100m,
                    PaymentMethod = "stripe",
                    PaymentId = session.Id,
                    CustomerEmail = session.CustomerDetails?.Email ?? session.CustomerEmail ?? string.Empty,
                    CouponId = string.IsNullOrEmpty(couponId) ? null : couponId,
                    CouponName = string.IsNullOrEmpty(couponName) ? null : couponName,
                    DiscountPercent = discountPercent,
                    OriginalPrice = originalPrice,
                    CpfNormalized = string.IsNullOrEmpty(cpfNormalized) ? null : cpfNormalized,
                    IncludeEnglish = includeEnglish,
                    EnglishPriceBRL = englishPrice,
                    AnalysisId = string.IsNullOrEmpty(analysisId) ? null : analysisId
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar webhook Stripe");
            }
        }
    }

    public async Task<PaymentProviderTestResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var secretKey = _configuration["STRIPE_SECRET_KEY"];
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            return new PaymentProviderTestResult
            {
                Connected = false,
                Provider = "stripe",
                Message = "STRIPE_SECRET_KEY não configurada no .env"
            };
        }

        try
        {
            var service = new BalanceService();
            var balance = await service.GetAsync(cancellationToken: cancellationToken);
            var mode = secretKey.StartsWith("sk_live", StringComparison.Ordinal) ? "live" : "test";
            return new PaymentProviderTestResult
            {
                Connected = true,
                Provider = "stripe",
                Message = $"Conexão com Stripe OK (modo {mode}).",
                Details = new { mode, currencies = balance.Available?.Select(b => b.Currency) }
            };
        }
        catch (Exception ex)
        {
            return new PaymentProviderTestResult
            {
                Connected = false,
                Provider = "stripe",
                Message = ex.Message
            };
        }
    }

    private static bool IsValidEmail(string email) =>
        !string.IsNullOrWhiteSpace(email) &&
        System.Text.RegularExpressions.Regex.IsMatch(email.Trim(), @"^[^\s@]+@[^\s@]+\.[^\s@]+$");
}
