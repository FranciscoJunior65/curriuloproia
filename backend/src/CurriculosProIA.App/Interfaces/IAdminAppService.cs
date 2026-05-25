using CurriculosProIA.Domain.Signatures.Admin;
using Microsoft.AspNetCore.Mvc;

namespace CurriculosProIA.App.Interfaces;

public interface IAdminAppService
{
    Task<IActionResult> GetDashboardStats(CancellationToken cancellationToken = default);
    Task<IActionResult> GetPaymentProviderSetting(CancellationToken cancellationToken = default);
    Task<IActionResult> UpdatePaymentProviderSetting(PaymentProviderUpdateSignature body, CancellationToken cancellationToken = default);
    Task<IActionResult> TestPaymentProviderConnection(PaymentProviderTestSignature? body, CancellationToken cancellationToken = default);
    Task<IActionResult> GetPricingSettings(CancellationToken cancellationToken = default);
    Task<IActionResult> UpdatePricingSettings(PricingConfigUpdateSignature body, CancellationToken cancellationToken = default);
    Task<IActionResult> GetDailyUsage(int days = 30, CancellationToken cancellationToken = default);
    Task<IActionResult> GetMonthlyUsage(int months = 12, CancellationToken cancellationToken = default);
    Task<IActionResult> GetSales(int limit = 100, int offset = 0, CancellationToken cancellationToken = default);
    Task<IActionResult> GetSalesStatistics(string? startDate, string? endDate, CancellationToken cancellationToken = default);
    IActionResult GetAiUsage(string period = "day");
    IActionResult GetJobSiteStats();
    IActionResult GetJobSiteDetailedStats(string siteId);
    Task<IActionResult> ListPartners(CancellationToken cancellationToken = default);
    Task<IActionResult> CreatePartner(CreatePartnerSignature body, CancellationToken cancellationToken = default);
    Task<IActionResult> ListCoupons(CancellationToken cancellationToken = default);
    Task<IActionResult> CreateCoupon(CreateCouponSignature body, CancellationToken cancellationToken = default);
    Task<IActionResult> UpdateCoupon(string couponId, UpdateCouponSignature body, CancellationToken cancellationToken = default);
    Task<IActionResult> GetCouponMetrics(CancellationToken cancellationToken = default);
}
