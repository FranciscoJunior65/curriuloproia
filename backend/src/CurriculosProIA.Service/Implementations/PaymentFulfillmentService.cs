using CurriculosProIA.Domain.Dtos;
using CurriculosProIA.Repository.Interfaces;
using CurriculosProIA.Service.Interfaces;
using Microsoft.Extensions.Logging;

namespace CurriculosProIA.Service.Implementations;

public class PaymentFulfillmentService : IPaymentFulfillmentService
{
    private readonly IPurchaseRepository _purchases;
    private readonly IUserProfileRepository _users;
    private readonly ICouponRepository _coupons;
    private readonly ICreditRepository _credits;
    private readonly IAnalysisRepository _analyses;
    private readonly IPricingService _pricing;
    private readonly IEmailService _email;
    private readonly ILogger<PaymentFulfillmentService> _logger;

    public PaymentFulfillmentService(
        IPurchaseRepository purchases,
        IUserProfileRepository users,
        ICouponRepository coupons,
        ICreditRepository credits,
        IAnalysisRepository analyses,
        IPricingService pricing,
        IEmailService email,
        ILogger<PaymentFulfillmentService> logger)
    {
        _purchases = purchases;
        _users = users;
        _coupons = coupons;
        _credits = credits;
        _analyses = analyses;
        _pricing = pricing;
        _email = email;
        _logger = logger;
    }

    public Task<FulfillOrderResult> FulfillFreeCheckoutAsync(
        FulfillOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        request.Price = 0;
        request.PaymentMethod = "coupon";
        request.PaymentId = $"free_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{request.UserId}";
        request.ExtraInfo = "Compra 100% grátis com cupom.";
        return FulfillPaidOrderAsync(request, cancellationToken);
    }

    public async Task<FulfillOrderResult> FulfillPaidOrderAsync(
        FulfillOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await _purchases.GetPurchaseByPaymentIdAsync(request.PaymentId, cancellationToken);
        if (existing != null)
        {
            var credits = await _credits.GetAvailableCreditsAsync(request.UserId, cancellationToken);
            var profile = await _users.GetUserProfileAsync(request.UserId, cancellationToken);
            return new FulfillOrderResult
            {
                AlreadyFulfilled = true,
                User = new PaymentUserSummary
                {
                    Id = request.UserId,
                    Credits = credits,
                    Plan = profile?.Plan
                }
            };
        }

        string? partnerId = null;
        decimal? partnerPercent = null;
        decimal? partnerAmount = null;

        if (!string.IsNullOrEmpty(request.CouponId))
        {
            var cupom = await _coupons.GetCouponByIdAsync(request.CouponId, cancellationToken);
            if (cupom != null && !string.IsNullOrEmpty(cupom.IdParceiro) && cupom.PorcentagemParceiro > 0)
            {
                partnerId = cupom.IdParceiro;
                partnerPercent = cupom.PorcentagemParceiro;
                partnerAmount = Math.Round(request.Price * cupom.PorcentagemParceiro.Value / 100m, 2);
            }
        }

        var isEnglishOnly = request.PlanId == "english";
        var config = await _pricing.GetPricingConfigAsync(cancellationToken);

        if (isEnglishOnly)
        {
            if (string.IsNullOrWhiteSpace(request.AnalysisId))
            {
                throw new InvalidOperationException("analysisId é obrigatório para compra de currículo em inglês.");
            }

            await _purchases.CreatePurchaseAsync(
                request.UserId,
                "english",
                "Currículo em Inglês",
                0,
                request.Price,
                request.PaymentMethod,
                request.PaymentId,
                serviceType: "curriculo_ingles",
                analysisId: request.AnalysisId,
                couponId: request.CouponId,
                couponName: request.CouponName,
                discountPercent: request.DiscountPercent,
                originalPrice: request.OriginalPrice,
                partnerId: partnerId,
                partnerPercent: partnerPercent,
                partnerAmount: partnerAmount,
                cancellationToken: cancellationToken);

            await _analyses.GrantEnglishPaidAsync(request.AnalysisId, cancellationToken);
        }
        else
        {
            var purchase = await _purchases.CreatePurchaseAsync(
                request.UserId,
                request.PlanId,
                request.PlanName,
                request.Analyses,
                request.Price,
                request.PaymentMethod,
                request.PaymentId,
                serviceType: "analysis_plan",
                couponId: request.CouponId,
                couponName: request.CouponName,
                discountPercent: request.DiscountPercent,
                originalPrice: request.OriginalPrice,
                partnerId: partnerId,
                partnerPercent: partnerPercent,
                partnerAmount: partnerAmount,
                cancellationToken: cancellationToken);

            if (request.IncludeEnglish)
            {
                var englishPrice = request.EnglishPriceBRL > 0
                    ? request.EnglishPriceBRL
                    : config.EnglishBundlePriceBRL;

                await _purchases.CreatePurchaseAsync(
                    request.UserId,
                    "english",
                    "Currículo em Inglês (bundle)",
                    0,
                    englishPrice,
                    request.PaymentMethod,
                    $"{request.PaymentId}_english",
                    parentPurchaseId: purchase.Id,
                    serviceType: "curriculo_ingles",
                    cancellationToken: cancellationToken);
            }
        }

        if (!string.IsNullOrEmpty(request.CouponId) && !string.IsNullOrEmpty(request.CpfNormalized))
        {
            await _coupons.RegisterCouponUseAsync(request.CouponId, request.CpfNormalized, cancellationToken);
        }

        if (!string.IsNullOrEmpty(request.CustomerEmail))
        {
            try
            {
                await _users.GetOrCreateUserProfileAsync(
                    request.UserId,
                    request.CustomerEmail,
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erro ao garantir perfil do usuário após pagamento");
            }
        }

        if (!isEnglishOnly)
        {
            await _users.UpdateUserProfileAsync(
                request.UserId,
                new Dictionary<string, object?> { ["plan"] = request.PlanId },
                cancellationToken);
        }

        var creditsAvailable = await _credits.GetAvailableCreditsAsync(request.UserId, cancellationToken);
        var userProfile = await _users.GetUserProfileAsync(request.UserId, cancellationToken);

        var recipientEmail = !string.IsNullOrWhiteSpace(request.CustomerEmail)
            ? request.CustomerEmail.Trim()
            : userProfile?.Email?.Trim();

        if (!string.IsNullOrWhiteSpace(recipientEmail))
        {
            try
            {
                await _email.SendPurchaseConfirmationEmailAsync(
                    recipientEmail,
                    new PurchaseConfirmationDetails
                    {
                        PlanName = request.PlanName,
                        CreditsAmount = isEnglishOnly ? null : request.Analyses,
                        Price = request.Price,
                        CustomerName = userProfile?.Name,
                        CouponName = request.CouponName,
                        DiscountPercent = request.DiscountPercent,
                        OriginalPrice = request.OriginalPrice,
                        ExtraInfo = string.IsNullOrWhiteSpace(request.ExtraInfo) ? null : request.ExtraInfo
                    },
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao enviar confirmação de compra para {Email}", recipientEmail);
            }
        }

        return new FulfillOrderResult
        {
            AlreadyFulfilled = false,
            User = new PaymentUserSummary
            {
                Id = request.UserId,
                Credits = creditsAvailable,
                Plan = userProfile?.Plan ?? request.PlanId
            }
        };
    }
}
