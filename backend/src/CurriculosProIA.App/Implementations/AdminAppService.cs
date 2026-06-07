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
    private readonly IPricingService _pricing;
    private readonly IInterviewConfigService _interviewConfig;
    private readonly IConfiguration _configuration;

    public AdminAppService(
        IAppDataStore data,
        ISettingsService settings,
        IPricingService pricing,
        IInterviewConfigService interviewConfig,
        IStripePaymentService stripe,
        IMercadoPagoService mercadoPago,
        IConfiguration configuration,
        IHttpContextAccessor http)
    {
        _http = http;
        _data = data;
        _settings = settings;
        _pricing = pricing;
        _interviewConfig = interviewConfig;
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
        var mercadoPagoMode = await _settings.GetMercadoPagoModeAsync(cancellationToken);
        return Ok(new
        {
            success = true,
            provider,
            providers = _settings.GetValidPaymentProviders(),
            labels = new { stripe = "Stripe", mercadopago = "Mercado Pago" },
            mercadoPagoMode,
            mercadoPagoModes = _settings.GetValidMercadoPagoModes(),
            mercadoPagoModeLabels = new { test = "Teste (sandbox)", production = "Produção (cobrança real)" }
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

            string? savedMpMode = null;
            if (!string.IsNullOrWhiteSpace(body.MercadoPagoMode))
            {
                savedMpMode = await _settings.SetMercadoPagoModeAsync(body.MercadoPagoMode, cancellationToken);
                _settings.ClearMercadoPagoModeCache();
            }

            var confirmed = await _settings.GetPaymentProviderAsync(cancellationToken);
            var confirmedMpMode = await _settings.GetMercadoPagoModeAsync(cancellationToken);
            var message = $"Meio de pagamento alterado para {(confirmed == "stripe" ? "Stripe" : "Mercado Pago")}.";
            if (savedMpMode != null)
            {
                message += confirmedMpMode == "production"
                    ? " Ambiente Mercado Pago: produção."
                    : " Ambiente Mercado Pago: teste.";
            }

            return Ok(new
            {
                success = true,
                message,
                provider = confirmed,
                mercadoPagoMode = confirmedMpMode
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, error = "Erro ao salvar configuração", message = ex.Message });
        }
    }

    public async Task<IActionResult> GetPricingSettings(CancellationToken cancellationToken)
    {
        if (!await EnsureAdminAsync(cancellationToken)) return AdminDenied();

        var config = await _pricing.GetPricingConfigAsync(cancellationToken);
        return Ok(new
        {
            success = true,
            config = new
            {
                creditUnitPriceBRL = config.CreditUnitPriceBRL,
                singleDiscountPercent = config.SingleDiscountPercent,
                pack3DiscountPercent = config.Pack3DiscountPercent,
                pack5DiscountPercent = config.Pack5DiscountPercent,
                englishPriceBRL = config.EnglishPriceBRL,
                englishBundlePriceBRL = config.EnglishBundlePriceBRL,
                singlePriceBRL = config.SinglePriceBRL,
                pack3PriceBRL = config.Pack3PriceBRL,
                pack5PriceBRL = config.Pack5PriceBRL
            }
        });
    }

    public async Task<IActionResult> UpdatePricingSettings(
        PricingConfigUpdateSignature body,
        CancellationToken cancellationToken)
    {
        if (!await EnsureAdminAsync(cancellationToken)) return AdminDenied();

        try
        {
            var current = await _pricing.GetPricingConfigAsync(cancellationToken);
            var updated = new PricingConfigDto
            {
                CreditUnitPriceBRL = body.CreditUnitPriceBRL ?? current.CreditUnitPriceBRL,
                SingleDiscountPercent = body.SingleDiscountPercent ?? current.SingleDiscountPercent,
                Pack3DiscountPercent = body.Pack3DiscountPercent ?? current.Pack3DiscountPercent,
                Pack5DiscountPercent = body.Pack5DiscountPercent ?? current.Pack5DiscountPercent,
                EnglishPriceBRL = body.EnglishPriceBRL ?? current.EnglishPriceBRL,
                EnglishBundlePriceBRL = body.EnglishBundlePriceBRL ?? current.EnglishBundlePriceBRL
            };

            var saved = await _pricing.SavePricingConfigAsync(updated, cancellationToken);
            _pricing.ClearCache();

            return Ok(new
            {
                success = true,
                message = "Preços atualizados com sucesso.",
                config = new
                {
                    creditUnitPriceBRL = saved.CreditUnitPriceBRL,
                    singleDiscountPercent = saved.SingleDiscountPercent,
                    pack3DiscountPercent = saved.Pack3DiscountPercent,
                    pack5DiscountPercent = saved.Pack5DiscountPercent,
                    englishPriceBRL = saved.EnglishPriceBRL,
                    englishBundlePriceBRL = saved.EnglishBundlePriceBRL,
                    singlePriceBRL = saved.SinglePriceBRL,
                    pack3PriceBRL = saved.Pack3PriceBRL,
                    pack5PriceBRL = saved.Pack5PriceBRL
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, error = "Erro ao salvar preços", message = ex.Message });
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

    public async Task<IActionResult> ListPartners(CancellationToken cancellationToken)
    {
        if (!await EnsureAdminAsync(cancellationToken)) return AdminDenied();

        var partners = await _data.ListPartnersAsync(cancellationToken);
        return Ok(new { success = true, partners });
    }

    public async Task<IActionResult> CreatePartner(CreatePartnerSignature body, CancellationToken cancellationToken)
    {
        if (!await EnsureAdminAsync(cancellationToken)) return AdminDenied();

        if (string.IsNullOrWhiteSpace(body.Nome))
        {
            return BadRequest(new { success = false, error = "Nome do parceiro é obrigatório." });
        }

        if (string.IsNullOrWhiteSpace(body.Cpf))
        {
            return BadRequest(new { success = false, error = "CPF ou CNPJ do parceiro é obrigatório." });
        }

        try
        {
            var partner = await _data.CreatePartnerAsync(
                body.Nome.Trim(),
                body.Cpf.Trim(),
                body.Descricao,
                body.Email,
                cancellationToken);
            return Ok(new { success = true, message = "Parceiro criado.", partner });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    public async Task<IActionResult> ListCoupons(CancellationToken cancellationToken)
    {
        if (!await EnsureAdminAsync(cancellationToken)) return AdminDenied();

        var coupons = await _data.ListCouponsAdminAsync(cancellationToken);
        EnrichCouponsWithPartnerLinks(coupons);
        return Ok(new { success = true, coupons });
    }

    public async Task<IActionResult> CreateCoupon(CreateCouponSignature body, CancellationToken cancellationToken)
    {
        if (!await EnsureAdminAsync(cancellationToken)) return AdminDenied();

        if (string.IsNullOrWhiteSpace(body.Nome))
        {
            return BadRequest(new { success = false, error = "Código do cupom é obrigatório." });
        }

        if (body.PorcentagemDesconto is null)
        {
            return BadRequest(new { success = false, error = "Porcentagem de desconto é obrigatória." });
        }

        try
        {
            var coupon = await _data.CreateCouponAsync(
                body.Nome.Trim(),
                body.PorcentagemDesconto.Value,
                body.ParceiroId,
                body.PorcentagemParceiro,
                cancellationToken);

            EnrichCouponsWithPartnerLinks(new List<AdminCouponDto> { coupon });

            return Ok(new { success = true, message = "Cupom criado.", coupon });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    public async Task<IActionResult> UpdateCoupon(
        string couponId,
        UpdateCouponSignature body,
        CancellationToken cancellationToken)
    {
        if (!await EnsureAdminAsync(cancellationToken)) return AdminDenied();

        try
        {
            var coupon = await _data.UpdateCouponAsync(
                couponId,
                body.PorcentagemDesconto,
                body.ParceiroId,
                body.PorcentagemParceiro,
                body.Ativo,
                body.ClearParceiro,
                cancellationToken);

            if (coupon == null)
            {
                return NotFound(new { success = false, error = "Cupom não encontrado." });
            }

            EnrichCouponsWithPartnerLinks(new List<AdminCouponDto> { coupon });

            return Ok(new { success = true, message = "Cupom atualizado.", coupon });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    public async Task<IActionResult> GetCouponMetrics(CancellationToken cancellationToken)
    {
        if (!await EnsureAdminAsync(cancellationToken)) return AdminDenied();

        var metrics = await _data.GetCouponMetricsAsync(cancellationToken);
        return Ok(new
        {
            success = true,
            metrics = new
            {
                byCoupon = metrics.ByCoupon,
                byPartner = metrics.ByPartner,
                totalPurchasesWithCoupon = metrics.TotalPurchasesWithCoupon,
                totalRevenueWithCoupon = metrics.TotalRevenueWithCoupon,
                totalPartnerPayout = metrics.TotalPartnerPayout
            }
        });
    }

    public async Task<IActionResult> ListPartnerReferrals(CancellationToken cancellationToken)
    {
        if (!await EnsureAdminAsync(cancellationToken)) return AdminDenied();

        var referrals = await _data.ListPartnerReferralsAsync(cancellationToken);
        var frontendUrl = GetFrontendUrl();
        foreach (var referral in referrals)
        {
            referral.PartnerLink = BuildPartnerLink(frontendUrl, referral.CouponCode);
        }

        return Ok(new { success = true, referrals });
    }

    public async Task<IActionResult> GetInterviewConfigSettings(CancellationToken cancellationToken)
    {
        if (!await EnsureAdminAsync(cancellationToken)) return AdminDenied();

        var config = await _interviewConfig.GetConfigAsync(cancellationToken);
        return Ok(new
        {
            success = true,
            config = new
            {
                introductionPrompt = config.IntroductionPrompt,
                questionsPrompt = config.QuestionsPrompt,
                feedbackPrompt = config.FeedbackPrompt,
                phase1Minutes = config.Phase1Minutes,
                phase2Minutes = config.Phase2Minutes,
                phase3Minutes = config.Phase3Minutes,
                maxVideoSpeechSeconds = config.MaxVideoSpeechSeconds,
                maxSegmentSeconds = config.MaxSegmentSeconds
            }
        });
    }

    public async Task<IActionResult> UpdateInterviewConfigSettings(
        InterviewConfigUpdateSignature body,
        CancellationToken cancellationToken)
    {
        if (!await EnsureAdminAsync(cancellationToken)) return AdminDenied();

        try
        {
            var current = await _interviewConfig.GetConfigAsync(cancellationToken);
            var updated = new InterviewConfigDto
            {
                IntroductionPrompt = body.IntroductionPrompt ?? current.IntroductionPrompt,
                QuestionsPrompt = body.QuestionsPrompt ?? current.QuestionsPrompt,
                FeedbackPrompt = body.FeedbackPrompt ?? current.FeedbackPrompt,
                Phase1Minutes = body.Phase1Minutes ?? current.Phase1Minutes,
                Phase2Minutes = body.Phase2Minutes ?? current.Phase2Minutes,
                Phase3Minutes = body.Phase3Minutes ?? current.Phase3Minutes,
                MaxVideoSpeechSeconds = body.MaxVideoSpeechSeconds ?? current.MaxVideoSpeechSeconds,
                MaxSegmentSeconds = body.MaxSegmentSeconds ?? current.MaxSegmentSeconds
            };

            var saved = await _interviewConfig.SaveConfigAsync(updated, cancellationToken);
            _interviewConfig.ClearCache();

            return Ok(new
            {
                success = true,
                message = "Configurações de entrevista atualizadas.",
                config = new
                {
                    introductionPrompt = saved.IntroductionPrompt,
                    questionsPrompt = saved.QuestionsPrompt,
                    feedbackPrompt = saved.FeedbackPrompt,
                    phase1Minutes = saved.Phase1Minutes,
                    phase2Minutes = saved.Phase2Minutes,
                    phase3Minutes = saved.Phase3Minutes,
                    maxVideoSpeechSeconds = saved.MaxVideoSpeechSeconds,
                    maxSegmentSeconds = saved.MaxSegmentSeconds
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    private string GetFrontendUrl() =>
        _configuration["FRONTEND_URL"]?.Trim().TrimEnd('/') ?? "http://localhost:4200";

    private static string BuildPartnerLink(string frontendUrl, string couponCode) =>
        $"{frontendUrl}/login?cupom={Uri.EscapeDataString(couponCode.Trim().ToUpperInvariant())}";

    private void EnrichCouponsWithPartnerLinks(IEnumerable<AdminCouponDto> coupons)
    {
        var frontendUrl = GetFrontendUrl();
        foreach (var coupon in coupons)
        {
            coupon.LinkParceiro = BuildPartnerLink(frontendUrl, coupon.Nome);
        }
    }

}
