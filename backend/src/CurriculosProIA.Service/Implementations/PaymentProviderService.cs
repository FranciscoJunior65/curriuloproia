using CurriculosProIA.Domain.Entities;
using CurriculosProIA.Domain.Dtos;
using CurriculosProIA.Repository.Persistence;
using CurriculosProIA.Domain.Dtos;

using CurriculosProIA.Service.Interfaces;
using CurriculosProIA.Repository.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CurriculosProIA.Service.Implementations;

public class PaymentProviderService : IPaymentProviderService
{
    private readonly ISettingsService _settings;
    private readonly IStripePaymentService _stripe;
    private readonly IMercadoPagoService _mercadoPago;
    private readonly ICaktoService _cakto;
    private readonly IKiwifyService _kiwify;
    private readonly IPaymentFulfillmentService _fulfillment;

    public PaymentProviderService(
        ISettingsService settings,
        IStripePaymentService stripe,
        IMercadoPagoService mercadoPago,
        ICaktoService cakto,
        IKiwifyService kiwify,
        IPaymentFulfillmentService fulfillment)
    {
        _settings = settings;
        _stripe = stripe;
        _mercadoPago = mercadoPago;
        _cakto = cakto;
        _kiwify = kiwify;
        _fulfillment = fulfillment;
    }

    public async Task<ProviderCheckoutResult> CreateProviderCheckoutAsync(
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
        _settings.ClearPaymentProviderCache();
        _settings.ClearMercadoPagoModeCache();
        var provider = await _settings.GetPaymentProviderAsync(cancellationToken);

        if (provider == "mercadopago")
        {
            var mp = await _mercadoPago.CreateCheckoutAsync(
                planId, userId, email, frontendUrl, couponCode, cpf, includeEnglish, analysisId, cancellationToken);
            return MapResult(provider, mp);
        }

        if (provider == "cakto")
        {
            var cakto = await _cakto.CreateCheckoutAsync(
                planId, userId, email, frontendUrl, couponCode, cpf, includeEnglish, analysisId, cancellationToken);
            return MapResult(provider, cakto);
        }

        if (provider == "kiwify")
        {
            var kiwify = await _kiwify.CreateCheckoutAsync(
                planId, userId, email, frontendUrl, couponCode, cpf, includeEnglish, analysisId, cancellationToken);
            return MapResult(provider, kiwify);
        }

        var stripe = await _stripe.CreateCheckoutSessionAsync(
            planId, userId, email, frontendUrl, couponCode, cpf, includeEnglish, analysisId, cancellationToken);
        return MapResult(provider, stripe);
    }

    public async Task<PaymentVerificationResult> VerifyProviderPaymentAsync(
        string sessionId,
        string? providerHint = null,
        CancellationToken cancellationToken = default)
    {
        var provider = providerHint ?? await _settings.GetPaymentProviderAsync(cancellationToken);

        if (provider == "mercadopago")
        {
            return await _mercadoPago.VerifyPaymentAsync(sessionId, cancellationToken);
        }

        if (provider == "cakto")
        {
            return await _cakto.VerifyPaymentAsync(sessionId, cancellationToken);
        }

        if (provider == "kiwify")
        {
            return await _kiwify.VerifyPaymentAsync(sessionId, cancellationToken);
        }

        var session = await _stripe.GetCheckoutSessionAsync(sessionId, cancellationToken);
        if (session.PaymentStatus != "paid")
        {
            return new PaymentVerificationResult
            {
                Paid = false,
                PaymentStatus = session.PaymentStatus
            };
        }

        session.Metadata.TryGetValue("userId", out var userId);
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
        session.Metadata.TryGetValue("analysisId", out var analysisIdMeta);

        decimal? discountPercent = decimal.TryParse(discountPercentStr, out var dp) ? dp : null;
        decimal? originalPrice = decimal.TryParse(originalPriceStr, out var op) ? op : null;
        var includeEnglishFlag = string.Equals(includeEnglishStr, "true", StringComparison.OrdinalIgnoreCase);
        decimal englishPrice = decimal.TryParse(englishPriceStr, out var ep) ? ep : 0;

        var result = await _fulfillment.FulfillPaidOrderAsync(new FulfillOrderRequest
        {
            UserId = userId ?? string.Empty,
            PlanId = planId ?? string.Empty,
            PlanName = planName ?? $"Plano {planId}",
            Analyses = int.TryParse(analysesStr, out var analyses) ? analyses : 0,
            Price = (session.AmountTotal ?? 0) / 100m,
            PaymentMethod = "stripe",
            PaymentId = session.Id,
            CustomerEmail = session.CustomerEmail ?? session.CustomerDetails?.Email ?? string.Empty,
            CouponId = string.IsNullOrEmpty(couponId) ? null : couponId,
            CouponName = string.IsNullOrEmpty(couponName) ? null : couponName,
            DiscountPercent = discountPercent,
            OriginalPrice = originalPrice,
            CpfNormalized = string.IsNullOrEmpty(cpfNormalized) ? null : cpfNormalized,
            IncludeEnglish = includeEnglishFlag,
            EnglishPriceBRL = englishPrice,
            AnalysisId = string.IsNullOrEmpty(analysisIdMeta) ? null : analysisIdMeta
        }, cancellationToken);

        return new PaymentVerificationResult
        {
            Paid = true,
            User = result.User,
            AlreadyFulfilled = result.AlreadyFulfilled
        };
    }

    private static ProviderCheckoutResult MapResult(string provider, CheckoutSessionResult result) =>
        new()
        {
            Provider = provider,
            FreeCheckout = result.FreeCheckout,
            TransparentCheckout = result.TransparentCheckout,
            AmountBRL = result.AmountBRL,
            PublicKey = result.PublicKey,
            PixAvailable = result.PixAvailable,
            SessionId = result.SessionId,
            Url = result.Url,
            PreferenceId = result.PreferenceId,
            UserId = result.UserId,
            PlanId = result.PlanId,
            PlanName = result.PlanName,
            Analyses = result.Analyses,
            CouponId = result.CouponId,
            CouponName = result.CouponName,
            DiscountPercent = result.DiscountPercent,
            OriginalPrice = result.OriginalPrice,
            CpfNormalized = result.CpfNormalized,
            LiveMode = result.LiveMode,
            PayerEmail = result.PayerEmail
        };
}
