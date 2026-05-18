using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using CurriculosProIA.Repository.Interfaces;
using CurriculosProIA.Service.Interfaces;
using CurriculosProIA.Domain.Entities;
using CurriculosProIA.Domain.Dtos;
using CurriculosProIA.Domain.Signatures.Auth;
using CurriculosProIA.Domain.Signatures.Analyze;
using CurriculosProIA.Domain.Signatures.Admin;
using CurriculosProIA.Domain.Signatures.Purchase;
using CurriculosProIA.App.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;


using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CurriculosProIA.App.Implementations;
using CurriculosProIA.App;
using CurriculosProIA.App.Interfaces;

public class AdminAppService : AppControllerBase, IAdminAppService 
{
    private readonly IHttpContextAccessor _http;
    private readonly IAppDataStore _data;
    private readonly ISettingsService _settings;
    private readonly IStripePaymentService _stripe;
    private readonly IMercadoPagoService _mercadoPago;
    private readonly IConfiguration _configuration;

    public AdminAppService(
        IAppDataStore data,
        ISettingsService settings,
        IStripePaymentService stripe,
        IMercadoPagoService mercadoPago,
        IConfiguration configuration,
        IHttpContextAccessor http)
    {
        _http = http;
        _data = data;
        _settings = settings;
        _stripe = stripe;
        _mercadoPago = mercadoPago;
        _configuration = configuration;
    }

        public async Task<IActionResult> GetDashboardStats(CancellationToken cancellationToken)
    {
        if (!await EnsureAdminAsync(cancellationToken)) return AdminDenied();

        try
        {
            var stats = await _data.GetAdminDashboardStatsAsync(cancellationToken);
            return Ok(new
            {
                success = true,
                stats = new
                {
                    totalUsers = stats.TotalUsers,
                    totalCredits = stats.TotalCredits,
                    creditsUsed = stats.CreditsUsed,
                    creditsAvailable = stats.CreditsAvailable,
                    analysesPerformed = stats.AnalysesPerformed,
                    estimatedRevenue = stats.EstimatedRevenue,
                    activeUsers = stats.ActiveUsers
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, error = "Erro ao obter estatísticas", message = ex.Message });
        }
    }

        public async Task<IActionResult> GetPaymentProviderSetting(CancellationToken cancellationToken)
    {
        if (!await EnsureAdminAsync(cancellationToken)) return AdminDenied();

        var provider = await _settings.GetPaymentProviderAsync(cancellationToken);
        return Ok(new
        {
            success = true,
            provider,
            providers = _settings.GetValidPaymentProviders(),
            labels = new { stripe = "Stripe", mercadopago = "Mercado Pago" }
        });
    }

        public async Task<IActionResult> UpdatePaymentProviderSetting(PaymentProviderUpdateSignature body, CancellationToken cancellationToken)
    {
        if (!await EnsureAdminAsync(cancellationToken)) return AdminDenied();

        if (string.IsNullOrEmpty(body.Provider))
        {
            return BadRequest(new { success = false, error = "Campo provider é obrigatório (stripe ou mercadopago)" });
        }

        try
        {
            var normalized = await _settings.SetPaymentProviderAsync(body.Provider, cancellationToken);
            _settings.ClearPaymentProviderCache();
            var confirmed = await _settings.GetPaymentProviderAsync(cancellationToken);
            return Ok(new
            {
                success = true,
                message = $"Meio de pagamento alterado para {(confirmed == "stripe" ? "Stripe" : "Mercado Pago")}.",
                provider = confirmed
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, error = "Erro ao salvar configuração", message = ex.Message });
        }
    }

        public async Task<IActionResult> TestPaymentProviderConnection(PaymentProviderTestSignature? body, CancellationToken cancellationToken)
    {
        if (!await EnsureAdminAsync(cancellationToken)) return AdminDenied();

        var provider = body?.Provider ?? await _settings.GetPaymentProviderAsync(cancellationToken);
        var result = provider == "mercadopago"
            ? await _mercadoPago.TestConnectionAsync(cancellationToken)
            : await _stripe.TestConnectionAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            connected = result.Connected,
            provider = result.Provider,
            message = result.Message,
            details = result.Details
        });
    }

        public async Task<IActionResult> GetDailyUsage(int days = 30, CancellationToken cancellationToken = default)
    {
        if (!await EnsureAdminAsync(cancellationToken)) return AdminDenied();
        return Ok(new { success = true, data = BuildEmptyDailyUsage(days) });
    }

        public async Task<IActionResult> GetMonthlyUsage(int months = 12, CancellationToken cancellationToken = default)
    {
        if (!await EnsureAdminAsync(cancellationToken)) return AdminDenied();
        return Ok(new { success = true, data = BuildEmptyMonthlyUsage(months) });
    }

        public async Task<IActionResult> GetSales(int limit = 100, int offset = 0, CancellationToken cancellationToken = default)
    {
        if (!await EnsureAdminAsync(cancellationToken)) return AdminDenied();

        var purchases = await _data.GetAllPurchasesAsync(limit, offset, cancellationToken);
        return Ok(new
        {
            success = true,
            purchases = purchases.Select(p => new
            {
                id = p.Id,
                userId = p.UserId,
                planId = p.PlanId,
                planName = p.PlanName,
                creditsAmount = p.CreditsAmount,
                price = p.Price,
                currency = p.Currency,
                status = p.Status,
                paymentMethod = p.PaymentMethod,
                paymentId = p.PaymentId,
                createdAt = p.CreatedAt,
                updatedAt = p.UpdatedAt
            }),
            total = purchases.Count,
            limit,
            offset
        });
    }

        public async Task<IActionResult> GetSalesStatistics(string? startDate, string? endDate, CancellationToken cancellationToken = default)
    {
        if (!await EnsureAdminAsync(cancellationToken)) return AdminDenied();

        var stats = await _data.GetSalesStatsAsync(startDate, endDate, cancellationToken);
        return Ok(new { success = true, stats });
    }

        public IActionResult GetAiUsage(string period = "day") =>
        Ok(new { success = true, stats = new { period, total = 0, successCount = 0, errorCount = 0 } });

        public IActionResult GetJobSiteStats() =>
        Ok(new { success = true, stats = Array.Empty<object>(), ranking = Array.Empty<object>(), total = 0 });

        public IActionResult GetJobSiteDetailedStats(string siteId) =>
        Ok(new { success = true, stats = Array.Empty<object>(), total = 0 });

    private async Task<bool> EnsureAdminAsync(CancellationToken cancellationToken)
    {
        var userId = JwtAuthHelper.TryGetUserId(_http.HttpContext!.Request.Headers, _configuration);
        if (string.IsNullOrEmpty(userId))
        {
            return false;
        }

        var profile = await _data.GetUserProfileAsync(userId, cancellationToken);
        return profile?.UserType == "admin";
    }

    private IActionResult AdminDenied() =>
        Unauthorized(new { success = false, error = "Token não fornecido" });

    private static IEnumerable<object> BuildEmptyDailyUsage(int days)
    {
        var end = DateTime.UtcNow.Date;
        var start = end.AddDays(-days);
        for (var d = start; d <= end; d = d.AddDays(1))
        {
            yield return new
            {
                date = d.ToString("yyyy-MM-dd"),
                registrations = 0,
                analyses = 0,
                revenue = 0
            };
        }
    }

    private static IEnumerable<object> BuildEmptyMonthlyUsage(int months)
    {
        var end = DateTime.UtcNow;
        var start = end.AddMonths(-months);
        for (var d = new DateTime(start.Year, start.Month, 1); d <= end; d = d.AddMonths(1))
        {
            yield return new
            {
                month = $"{d.Year}-{d.Month:D2}",
                registrations = 0,
                analyses = 0,
                revenue = 0
            };
        }
    }

}
