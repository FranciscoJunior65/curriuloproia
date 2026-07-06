using CurriculosProIA.App.Interfaces;
using CurriculosProIA.Domain.Signatures.Admin;
using Microsoft.AspNetCore.Mvc;

namespace CurriculosProIA.Api.Controllers;

[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly IAdminAppService _admin;

    public AdminController(IAdminAppService admin) => _admin = admin;

    [HttpGet("stats")]
    public Task<IActionResult> GetDashboardStats(CancellationToken ct) => _admin.GetDashboardStats(ct);

    [HttpGet("settings/payment-provider")]
    public Task<IActionResult> GetPaymentProviderSetting(CancellationToken ct) => _admin.GetPaymentProviderSetting(ct);

    [HttpPut("settings/payment-provider")]
    public Task<IActionResult> UpdatePaymentProviderSetting([FromBody] PaymentProviderUpdateSignature body, CancellationToken ct) =>
        _admin.UpdatePaymentProviderSetting(body, ct);

    [HttpPost("settings/payment-provider/test")]
    public Task<IActionResult> TestPaymentProviderConnection([FromBody] PaymentProviderTestSignature? body, CancellationToken ct) =>
        _admin.TestPaymentProviderConnection(body, ct);

    [HttpGet("settings/pricing")]
    public Task<IActionResult> GetPricingSettings(CancellationToken ct) => _admin.GetPricingSettings(ct);

    [HttpPut("settings/pricing")]
    public Task<IActionResult> UpdatePricingSettings([FromBody] PricingConfigUpdateSignature body, CancellationToken ct) =>
        _admin.UpdatePricingSettings(body, ct);

    [HttpGet("usage/daily")]
    public Task<IActionResult> GetDailyUsage([FromQuery] int days = 30, CancellationToken ct = default) =>
        _admin.GetDailyUsage(days, ct);

    [HttpGet("usage/monthly")]
    public Task<IActionResult> GetMonthlyUsage([FromQuery] int months = 12, CancellationToken ct = default) =>
        _admin.GetMonthlyUsage(months, ct);

    [HttpGet("sales")]
    public Task<IActionResult> GetSales([FromQuery] int limit = 100, [FromQuery] int offset = 0, CancellationToken ct = default) =>
        _admin.GetSales(limit, offset, ct);

    [HttpGet("sales/statistics")]
    public Task<IActionResult> GetSalesStatistics([FromQuery] string? startDate, [FromQuery] string? endDate, CancellationToken ct = default) =>
        _admin.GetSalesStatistics(startDate, endDate, ct);

    [HttpGet("ai-usage")]
    public IActionResult GetAiUsage([FromQuery] string period = "day") => _admin.GetAiUsage(period);

    [HttpGet("job-sites/stats")]
    public IActionResult GetJobSiteStats() => _admin.GetJobSiteStats();

    [HttpGet("job-sites/{siteId}/stats")]
    public IActionResult GetJobSiteDetailedStats(string siteId) => _admin.GetJobSiteDetailedStats(siteId);

    [HttpGet("partners")]
    public Task<IActionResult> ListPartners(CancellationToken ct) => _admin.ListPartners(ct);

    [HttpPost("partners")]
    public Task<IActionResult> CreatePartner([FromBody] CreatePartnerSignature body, CancellationToken ct) =>
        _admin.CreatePartner(body, ct);

    [HttpGet("coupons")]
    public Task<IActionResult> ListCoupons(CancellationToken ct) => _admin.ListCoupons(ct);

    [HttpPost("coupons")]
    public Task<IActionResult> CreateCoupon([FromBody] CreateCouponSignature body, CancellationToken ct) =>
        _admin.CreateCoupon(body, ct);

    [HttpPut("coupons/{couponId}")]
    public Task<IActionResult> UpdateCoupon(string couponId, [FromBody] UpdateCouponSignature body, CancellationToken ct) =>
        _admin.UpdateCoupon(couponId, body, ct);

    [HttpGet("coupons/metrics")]
    public Task<IActionResult> GetCouponMetrics(CancellationToken ct) => _admin.GetCouponMetrics(ct);

    [HttpGet("partner-referrals")]
    public Task<IActionResult> ListPartnerReferrals(CancellationToken ct) => _admin.ListPartnerReferrals(ct);

    [HttpGet("settings/interview-config")]
    public Task<IActionResult> GetInterviewConfigSettings(CancellationToken ct) =>
        _admin.GetInterviewConfigSettings(ct);

    [HttpPut("settings/interview-config")]
    public Task<IActionResult> UpdateInterviewConfigSettings([FromBody] InterviewConfigUpdateSignature body, CancellationToken ct) =>
        _admin.UpdateInterviewConfigSettings(body, ct);

    [HttpGet("kiwify/sales/{orderId}")]
    public Task<IActionResult> GetKiwifySale(string orderId, CancellationToken ct) =>
        _admin.GetKiwifySale(orderId, ct);

    [HttpGet("kiwify/webhook-logs")]
    public Task<IActionResult> ListKiwifyWebhookLogs(
        [FromQuery] string? orderId,
        [FromQuery] string? orderRef,
        [FromQuery] int limit = 50,
        CancellationToken ct = default) =>
        _admin.ListKiwifyWebhookLogs(orderId, orderRef, limit, ct);

    [HttpPost("kiwify/reconcile")]
    public Task<IActionResult> ReconcileKiwifyOrder([FromBody] AdminReconcileKiwifySignature body, CancellationToken ct) =>
        _admin.ReconcileKiwifyOrder(body, ct);

    [HttpPost("kiwify/webhook")]
    public Task<IActionResult> ProcessKiwifyWebhook([FromBody] AdminProcessKiwifyWebhookSignature body, CancellationToken ct) =>
        _admin.ProcessKiwifyWebhook(body, ct);

    [HttpGet("users/search")]
    public Task<IActionResult> SearchUsers([FromQuery] string q, [FromQuery] int limit = 20, CancellationToken ct = default) =>
        _admin.SearchUsers(q, limit, ct);

    [HttpGet("purchases/pending")]
    public Task<IActionResult> ListPendingPurchases([FromQuery] string? userId, [FromQuery] int limit = 50, CancellationToken ct = default) =>
        _admin.ListPendingPurchases(userId, limit, ct);

    [HttpGet("purchases/buyers")]
    public Task<IActionResult> ListPurchaseBuyers([FromQuery] int limit = 300, CancellationToken ct = default) =>
        _admin.ListPurchaseBuyers(limit, ct);

    [HttpPost("purchases/pending")]
    public Task<IActionResult> CreatePendingPurchase([FromBody] AdminPendingPurchaseSignature body, CancellationToken ct) =>
        _admin.CreatePendingPurchase(body, ct);

    [HttpPost("credits/grant")]
    public Task<IActionResult> GrantManualCredits([FromBody] AdminGrantCreditsSignature body, CancellationToken ct) =>
        _admin.GrantManualCredits(body, ct);
}
