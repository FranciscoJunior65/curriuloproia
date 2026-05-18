using CurriculosProIA.Domain.Signatures.Admin;
using Microsoft.AspNetCore.Mvc;

namespace CurriculosProIA.App.Interfaces;

public interface IAdminAppService
{
    Task<IActionResult> GetDashboardStats(CancellationToken cancellationToken = default);
    Task<IActionResult> GetPaymentProviderSetting(CancellationToken cancellationToken = default);
    Task<IActionResult> UpdatePaymentProviderSetting(PaymentProviderUpdateSignature body, CancellationToken cancellationToken = default);
    Task<IActionResult> TestPaymentProviderConnection(PaymentProviderTestSignature? body, CancellationToken cancellationToken = default);
    Task<IActionResult> GetDailyUsage(int days = 30, CancellationToken cancellationToken = default);
    Task<IActionResult> GetMonthlyUsage(int months = 12, CancellationToken cancellationToken = default);
    Task<IActionResult> GetSales(int limit = 100, int offset = 0, CancellationToken cancellationToken = default);
    Task<IActionResult> GetSalesStatistics(string? startDate, string? endDate, CancellationToken cancellationToken = default);
    IActionResult GetAiUsage(string period = "day");
    IActionResult GetJobSiteStats();
    IActionResult GetJobSiteDetailedStats(string siteId);
}
