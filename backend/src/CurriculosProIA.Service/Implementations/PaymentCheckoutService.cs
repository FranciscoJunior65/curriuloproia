using CurriculosProIA.Domain.Entities;
using CurriculosProIA.Domain.Dtos;
using CurriculosProIA.Repository.Persistence;
using CurriculosProIA.Domain.Dtos;

using CurriculosProIA.Service.Interfaces;
using CurriculosProIA.Repository.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

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
        CancellationToken cancellationToken = default)
    {
        var plan = _pricing.GetPlan(planId)
            ?? throw new InvalidOperationException("Plano não encontrado");

        var amountBrl = plan.PriceBRL;
        var metadata = new Dictionary<string, string>
        {
            ["userId"] = userId,
            ["planId"] = planId,
            ["planName"] = plan.Name,
            ["analyses"] = plan.Analyses.ToString()
        };

        CheckoutCouponInfo? couponInfo = null;

        if (!string.IsNullOrWhiteSpace(couponCode))
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
            var original = plan.PriceBRL;
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
                PlanName = plan.Name,
                Analyses = plan.Analyses,
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
            PlanName = plan.Name,
            Analyses = plan.Analyses,
            AmountBRL = amountBrl,
            AmountInCents = amountInCents,
            Plan = plan,
            Metadata = metadata,
            CouponInfo = couponInfo
        };
    }
}
