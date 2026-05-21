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
}
