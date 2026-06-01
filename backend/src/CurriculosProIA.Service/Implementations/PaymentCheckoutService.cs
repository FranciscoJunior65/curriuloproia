using CurriculosProIA.Domain.Dtos;
using CurriculosProIA.Domain.Entities;
using CurriculosProIA.Repository.Interfaces;
using CurriculosProIA.Service.Interfaces;

namespace CurriculosProIA.Service.Implementations;

public class PaymentCheckoutService : IPaymentCheckoutService
{
    private readonly IPricingService _pricing;
    private readonly ICouponRepository _coupons;

    public PaymentCheckoutService(IPricingService pricing, ICouponRepository coupons)
    {
        _pricing = pricing;
        _coupons = coupons;
    }

    public async Task<CheckoutContext> BuildCheckoutContextAsync(
        string planId,
        string userId,
        string? couponCode = null,
        string? cpf = null,
        bool includeEnglish = false,
        string? analysisId = null,
        CancellationToken cancellationToken = default)
    {
        var config = await _pricing.GetPricingConfigAsync(cancellationToken);
        PricingPlan plan;

        if (planId == "english")
        {
            if (string.IsNullOrWhiteSpace(analysisId))
            {
                throw new InvalidOperationException(
                    "É necessário informar a análise para comprar o currículo em inglês.");
            }

            plan = await _pricing.GetPlanAsync("english", cancellationToken)
                ?? new PricingPlan
                {
                    Id = "english",
                    Name = "Currículo em Inglês",
                    Analyses = 0,
                    PriceBRL = config.EnglishPriceBRL
                };
            plan.PriceBRL = config.EnglishPriceBRL;
        }
        else
        {
            plan = await _pricing.GetPlanAsync(planId, cancellationToken)
                ?? throw new InvalidOperationException("Plano não encontrado");
        }

        var amountBrl = plan.PriceBRL;
        var planName = plan.Name;
        var analyses = plan.Analyses;
        var englishAddOn = 0m;

        if (planId != "english" && includeEnglish)
        {
            englishAddOn = config.EnglishBundlePriceBRL;
            amountBrl += englishAddOn;
            planName += " + Currículo em Inglês";
        }

        var metadata = new Dictionary<string, string>
        {
            ["userId"] = userId,
            ["planId"] = planId,
            ["planName"] = planName,
            ["analyses"] = analyses.ToString()
        };

        if (includeEnglish && planId != "english")
        {
            metadata["includeEnglish"] = "true";
            metadata["englishPriceBRL"] = englishAddOn.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (!string.IsNullOrWhiteSpace(analysisId))
        {
            metadata["analysisId"] = analysisId.Trim();
        }

        CheckoutCouponInfo? couponInfo = null;

        if (!string.IsNullOrWhiteSpace(couponCode) && planId != "english")
        {
            var cpfNorm = cpf != null ? _coupons.NormalizeCpf(cpf) : string.Empty;
            if (cpfNorm.Length != 11)
            {
                throw new InvalidOperationException("Para usar cupom, informe seu CPF (11 dígitos).");
            }

            var result = await _coupons.ValidateCouponAsync(couponCode.Trim(), cpf, cancellationToken);
            if (!result.Valid || result.Coupon == null)
            {
                throw new InvalidOperationException(result.Message ?? "Cupom inválido ou já utilizado por este CPF.");
            }

            var pct = (decimal)result.Coupon.PorcentagemDesconto;
            var original = amountBrl;
            amountBrl = Math.Max(0, original * (1 - pct / 100));
            couponInfo = new CheckoutCouponInfo
            {
                CouponId = result.Coupon.Id,
                CouponName = result.Coupon.Nome ?? string.Empty,
                DiscountPercent = pct,
                OriginalPrice = original
            };

            metadata["couponId"] = couponInfo.CouponId;
            metadata["couponName"] = couponInfo.CouponName;
            metadata["discountPercent"] = pct.ToString(System.Globalization.CultureInfo.InvariantCulture);
            metadata["originalPrice"] = original.ToString(System.Globalization.CultureInfo.InvariantCulture);
            metadata["cpfNormalized"] = cpfNorm;
        }

        var amountInCents = (long)Math.Round(amountBrl * 100);

        if (amountInCents <= 0 && couponInfo != null)
        {
            return new CheckoutContext
            {
                FreeCheckout = true,
                UserId = userId,
                PlanId = planId,
                PlanName = planName,
                Analyses = analyses,
                AmountBRL = 0,
                Plan = plan,
                Metadata = metadata,
                CouponInfo = couponInfo,
                CpfNormalized = metadata.GetValueOrDefault("cpfNormalized")
            };
        }

        return new CheckoutContext
        {
            FreeCheckout = false,
            UserId = userId,
            PlanId = planId,
            PlanName = planName,
            Analyses = analyses,
            AmountBRL = amountBrl,
            AmountInCents = amountInCents,
            Plan = plan,
            Metadata = metadata,
            CouponInfo = couponInfo
        };
    }
}
