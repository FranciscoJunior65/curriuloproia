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
using CurriculosProIA.Service.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

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
    private readonly ICaktoService _cakto;
    private readonly IKiwifyService _kiwify;
    private readonly IKiwifyWebhookLogRepository _kiwifyWebhookLogs;
    private readonly IPaymentFulfillmentService _fulfillment;
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
        ICaktoService cakto,
        IKiwifyService kiwify,
        IKiwifyWebhookLogRepository kiwifyWebhookLogs,
        IPaymentFulfillmentService fulfillment,
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
        _cakto = cakto;
        _kiwify = kiwify;
        _kiwifyWebhookLogs = kiwifyWebhookLogs;
        _fulfillment = fulfillment;
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
            labels = new { stripe = "Stripe", mercadopago = "Mercado Pago", cakto = "Cakto", kiwify = "Kiwify" },
            mercadoPagoMode,
            mercadoPagoModes = _settings.GetValidMercadoPagoModes(),
            mercadoPagoModeLabels = new { test = "Teste (sandbox)", production = "Produção (cobrança real)" },
            mercadoPagoProductionHint =
                "Produção exige MERCADOPAGO_ACCESS_TOKEN_PRODUCTION no backend/.env (token real, diferente do de teste)."
        });
    }

        public async Task<IActionResult> UpdatePaymentProviderSetting(PaymentProviderUpdateSignature body, CancellationToken cancellationToken)
    {
        if (!await EnsureAdminAsync(cancellationToken)) return AdminDenied();

        if (string.IsNullOrEmpty(body.Provider))
        {
            return BadRequest(new { success = false, error = "Campo provider é obrigatório (stripe, mercadopago, cakto ou kiwify)" });
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
            var message = confirmed switch
            {
                "stripe" => "Meio de pagamento alterado para Stripe.",
                "cakto" => "Meio de pagamento alterado para Cakto.",
                "kiwify" => "Meio de pagamento alterado para Kiwify.",
                _ => "Meio de pagamento alterado para Mercado Pago."
            };
            if (savedMpMode != null)
            {
                message += confirmedMpMode == "production"
                    ? " Ambiente Mercado Pago: produção (cobrança real)."
                    : " Ambiente Mercado Pago: teste (sandbox).";
            }

            string? warning = null;
            if (confirmed == "mercadopago" && confirmedMpMode == "production")
            {
                var mpTest = await _mercadoPago.TestConnectionAsync(cancellationToken);
                if (!mpTest.Connected)
                {
                    warning = mpTest.Message +
                              " Configure MERCADOPAGO_ACCESS_TOKEN_PRODUCTION no backend/.env com o token de produção e republique a API.";
                }
            }

            return Ok(new
            {
                success = true,
                message,
                warning,
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
                transactionFeeBRL = config.TransactionFeeBRL,
                singlePriceBRL = config.SinglePriceBRL,
                pack3PriceBRL = config.Pack3PriceBRL,
                pack5PriceBRL = config.Pack5PriceBRL
            },
            kiwifyProducts = BuildKiwifyProductMap(config)
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
                EnglishBundlePriceBRL = body.EnglishBundlePriceBRL ?? current.EnglishBundlePriceBRL,
                TransactionFeeBRL = body.TransactionFeeBRL ?? current.TransactionFeeBRL
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
                    transactionFeeBRL = saved.TransactionFeeBRL,
                    singlePriceBRL = saved.SinglePriceBRL,
                    pack3PriceBRL = saved.Pack3PriceBRL,
                    pack5PriceBRL = saved.Pack5PriceBRL
                },
                kiwifyProducts = BuildKiwifyProductMap(saved)
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
        var result = provider switch
        {
            "mercadopago" => await _mercadoPago.TestConnectionAsync(cancellationToken),
            "cakto" => await _cakto.TestConnectionAsync(cancellationToken),
            "kiwify" => await _kiwify.TestConnectionAsync(cancellationToken),
            _ => await _stripe.TestConnectionAsync(cancellationToken)
        };

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
        var data = await _data.GetDailyUsageAsync(days, cancellationToken);
        return Ok(new { success = true, data });
    }

        public async Task<IActionResult> GetMonthlyUsage(int months = 12, CancellationToken cancellationToken = default)
    {
        if (!await EnsureAdminAsync(cancellationToken)) return AdminDenied();
        var data = await _data.GetMonthlyUsageAsync(months, cancellationToken);
        return Ok(new { success = true, data });
    }

        public async Task<IActionResult> GetSales(int limit = 100, int offset = 0, CancellationToken cancellationToken = default)
    {
        if (!await EnsureAdminAsync(cancellationToken)) return AdminDenied();

        var purchases = await _data.GetAllPurchasesAsync(limit, offset, cancellationToken);
        var userIds = purchases
            .Select(p => p.UserId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToList();

        var userProfiles = new Dictionary<string, UserProfile>(StringComparer.OrdinalIgnoreCase);
        foreach (var userId in userIds)
        {
            var profile = await _data.GetUserProfileAsync(userId, cancellationToken);
            if (profile != null)
            {
                userProfiles[userId] = profile;
            }
        }

        return Ok(new
        {
            success = true,
            purchases = purchases.Select(p => new
            {
                userName = !string.IsNullOrWhiteSpace(p.UserId) && userProfiles.TryGetValue(p.UserId, out var profile)
                    ? (string.IsNullOrWhiteSpace(profile.Name) ? profile.Email : profile.Name)
                    : null,
                userEmail = !string.IsNullOrWhiteSpace(p.UserId) && userProfiles.TryGetValue(p.UserId, out profile)
                    ? profile.Email
                    : null,
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

    public async Task<IActionResult> GetKiwifySale(string orderId, CancellationToken cancellationToken)
    {
        if (!await EnsureAdminAsync(cancellationToken)) return AdminDenied();

        if (string.IsNullOrWhiteSpace(orderId))
        {
            return BadRequest(new { success = false, error = "orderId é obrigatório" });
        }

        try
        {
            var sale = await _kiwify.GetSaleDetailsAsync(orderId.Trim(), cancellationToken);
            return Ok(new { success = true, sale });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, error = "Erro ao consultar venda Kiwify", message = ex.Message });
        }
    }

    public async Task<IActionResult> ListKiwifyWebhookLogs(
        string? orderId,
        string? orderRef,
        int limit,
        CancellationToken cancellationToken)
    {
        if (!await EnsureAdminAsync(cancellationToken)) return AdminDenied();

        limit = Math.Clamp(limit <= 0 ? 50 : limit, 1, 200);
        var logs = await _kiwifyWebhookLogs.ListAsync(
            string.IsNullOrWhiteSpace(orderId) ? null : orderId.Trim(),
            string.IsNullOrWhiteSpace(orderRef) ? null : orderRef.Trim(),
            limit,
            cancellationToken);

        return Ok(new { success = true, logs });
    }

    public async Task<IActionResult> ReconcileKiwifyOrder(
        AdminReconcileKiwifySignature body,
        CancellationToken cancellationToken)
    {
        if (!await EnsureAdminAsync(cancellationToken)) return AdminDenied();

        if (string.IsNullOrWhiteSpace(body.OrderId))
        {
            return BadRequest(new { success = false, error = "orderId é obrigatório (order_ref ou order_id da Kiwify)" });
        }

        try
        {
            var details = await _kiwify.GetSaleDetailsAsync(body.OrderId.Trim(), cancellationToken);
            if (!details.Paid)
            {
                return Ok(new
                {
                    success = true,
                    processed = false,
                    paid = false,
                    message = "Venda ainda não está paga/aprovada na Kiwify.",
                    sale = details
                });
            }

            if (details.AlreadyFulfilled)
            {
                return Ok(new
                {
                    success = true,
                    processed = false,
                    paid = true,
                    alreadyFulfilled = true,
                    message = "Esta venda já foi baixada no sistema.",
                    sale = details
                });
            }

            var result = await _kiwify.ReconcileOrderAsync(body.OrderId.Trim(), cancellationToken);
            if (result.Paid && result.User != null)
            {
                var meta = TryParseExternalReference(details.ExternalReference);
                var planId = meta?.P ?? "single";
                await _data.MarkPendingPurchasesSubstitutedAsync(result.User.Id, planId, cancellationToken);

                if (!string.IsNullOrWhiteSpace(body.PendingPurchaseId))
                {
                    await _data.UpdatePurchaseStatusAsync(
                        body.PendingPurchaseId.Trim(),
                        "substituida",
                        details.PaymentIdUsed,
                        cancellationToken);
                }
            }

            return Ok(new
            {
                success = true,
                processed = result.Paid && !result.AlreadyFulfilled,
                paid = result.Paid,
                alreadyFulfilled = result.AlreadyFulfilled,
                credits = result.User?.Credits,
                userId = result.User?.Id,
                sale = details
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, error = "Erro ao conciliar venda Kiwify", message = ex.Message });
        }
    }

    public async Task<IActionResult> ListPendingPurchases(
        string? userId,
        int limit,
        CancellationToken cancellationToken)
    {
        if (!await EnsureAdminAsync(cancellationToken)) return AdminDenied();

        limit = Math.Clamp(limit <= 0 ? 50 : limit, 1, 200);
        var purchases = await _data.GetPendingPurchasesAsync(userId, limit, cancellationToken);
        var items = new List<object>();

        foreach (var purchase in purchases)
        {
            var profile = !string.IsNullOrWhiteSpace(purchase.UserId)
                ? await _data.GetUserProfileAsync(purchase.UserId, cancellationToken)
                : null;

            items.Add(new
            {
                id = purchase.Id,
                userId = purchase.UserId,
                userName = string.IsNullOrWhiteSpace(profile?.Name) ? profile?.Email : profile?.Name,
                userEmail = profile?.Email,
                planId = purchase.PlanId,
                planName = purchase.PlanName,
                creditsAmount = purchase.CreditsAmount,
                price = purchase.Price,
                paymentMethod = purchase.PaymentMethod,
                paymentId = purchase.PaymentId,
                status = purchase.Status,
                createdAt = purchase.CreatedAt
            });
        }

        return Ok(new { success = true, purchases = items });
    }

    public async Task<IActionResult> ListPurchaseBuyers(
        int limit,
        CancellationToken cancellationToken)
    {
        if (!await EnsureAdminAsync(cancellationToken)) return AdminDenied();

        limit = Math.Clamp(limit <= 0 ? 300 : limit, 1, 500);
        var buyers = await _data.GetDistinctPurchaseBuyersAsync(limit, cancellationToken);

        return Ok(new
        {
            success = true,
            buyers = buyers.Select(b => new
            {
                id = b.Id,
                email = b.Email,
                name = b.Name,
                credits = b.Credits,
                purchasesCount = b.PurchasesCount,
                lastPurchaseAt = b.LastPurchaseAt
            })
        });
    }

    public async Task<IActionResult> CreatePendingPurchase(
        AdminPendingPurchaseSignature body,
        CancellationToken cancellationToken)
    {
        if (!await EnsureAdminAsync(cancellationToken)) return AdminDenied();

        var user = await ResolveTargetUserAsync(body.UserId, body.Email, cancellationToken);
        if (user == null)
        {
            return NotFound(new { success = false, error = "Usuário não encontrado (informe userId ou email)" });
        }

        var planId = body.PlanId?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(planId))
        {
            return BadRequest(new { success = false, error = "planId é obrigatório (single, pack3, pack5)" });
        }

        var plan = await _pricing.GetPlanAsync(planId, cancellationToken);
        if (plan == null)
        {
            return BadRequest(new { success = false, error = "planId inválido" });
        }

        var kiwifyOrderId = body.KiwifyOrderId?.Trim();
        var purchase = await _data.CreatePendingPurchaseAsync(
            user.Id,
            plan.Id,
            plan.Name,
            plan.Analyses,
            plan.PriceBRL,
            paymentMethod: "kiwify",
            paymentId: string.IsNullOrWhiteSpace(kiwifyOrderId) ? null : kiwifyOrderId,
            cancellationToken: cancellationToken);

        return Ok(new
        {
            success = true,
            message = "Solicitação de compra registrada como pendente.",
            purchase = new
            {
                id = purchase.Id,
                userId = purchase.UserId,
                userName = string.IsNullOrWhiteSpace(user.Name) ? user.Email : user.Name,
                userEmail = user.Email,
                planId = purchase.PlanId,
                planName = purchase.PlanName,
                creditsAmount = purchase.CreditsAmount,
                price = purchase.Price,
                paymentId = purchase.PaymentId,
                status = purchase.Status,
                createdAt = purchase.CreatedAt
            }
        });
    }

    public async Task<IActionResult> GrantManualCredits(
        AdminGrantCreditsSignature body,
        CancellationToken cancellationToken)
    {
        if (!await EnsureAdminAsync(cancellationToken)) return AdminDenied();

        var user = await ResolveTargetUserAsync(body.UserId, body.Email, cancellationToken);
        if (user == null)
        {
            return NotFound(new { success = false, error = "Usuário não encontrado (informe userId ou email)" });
        }

        string planId;
        string planName;
        int analyses;
        decimal price;

        if (body.Credits is > 0)
        {
            analyses = body.Credits.Value;
            planId = string.IsNullOrWhiteSpace(body.PlanId) ? "single" : body.PlanId.Trim();
            planName = $"{analyses} crédito(s) — inclusão manual admin";
            price = body.Price ?? 0;
        }
        else
        {
            planId = body.PlanId?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(planId))
            {
                return BadRequest(new { success = false, error = "Informe planId ou credits" });
            }

            var plan = await _pricing.GetPlanAsync(planId, cancellationToken);
            if (plan == null)
            {
                return BadRequest(new { success = false, error = "planId inválido" });
            }

            planName = plan.Name;
            analyses = plan.Analyses;
            price = body.Price ?? plan.PriceBRL;
        }

        var reason = string.IsNullOrWhiteSpace(body.Reason)
            ? "Créditos incluídos manualmente pelo administrador."
            : body.Reason.Trim();
        var paymentMethod = string.IsNullOrWhiteSpace(body.PaymentMethod)
            ? "admin_manual"
            : body.PaymentMethod.Trim();
        var paymentId = string.IsNullOrWhiteSpace(body.PaymentId)
            ? $"admin_manual_{Guid.NewGuid():N}"
            : body.PaymentId.Trim();

        var result = await _fulfillment.FulfillPaidOrderAsync(
            new FulfillOrderRequest
            {
                UserId = user.Id,
                PlanId = planId,
                PlanName = planName,
                Analyses = analyses,
                Price = price,
                PaymentMethod = paymentMethod,
                PaymentId = paymentId,
                CustomerEmail = user.Email ?? string.Empty,
                ExtraInfo = reason,
                SendConfirmationEmail = body.SendEmail
            },
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(body.PendingPurchaseId))
        {
            await _data.UpdatePurchaseStatusAsync(
                body.PendingPurchaseId.Trim(),
                "substituida",
                paymentId,
                cancellationToken);
        }

        return Ok(new
        {
            success = true,
            message = result.AlreadyFulfilled
                ? "Esta inclusão já havia sido registrada."
                : $"{analyses} crédito(s) incluído(s) com sucesso.",
            alreadyFulfilled = result.AlreadyFulfilled,
            credits = result.User?.Credits,
            userId = user.Id,
            userEmail = user.Email
        });
    }

    public async Task<IActionResult> SearchUsers(
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        if (!await EnsureAdminAsync(cancellationToken)) return AdminDenied();

        var users = await _data.SearchUsersAsync(query, limit, cancellationToken);
        return Ok(new
        {
            success = true,
            users = users.Select(u => new
            {
                id = u.Id,
                email = u.Email,
                name = u.Name,
                credits = u.Credits
            })
        });
    }

    public async Task<IActionResult> ListUsers(
        int limit,
        int offset,
        string? search,
        CancellationToken cancellationToken)
    {
        if (!await EnsureAdminAsync(cancellationToken)) return AdminDenied();

        var users = await _data.ListUsersAsync(limit, offset, search, cancellationToken);
        return Ok(new
        {
            success = true,
            users = users.Select(u => new
            {
                id = u.Id,
                email = u.Email,
                name = u.Name,
                cpf = u.Cpf,
                userType = u.UserType,
                credits = u.Credits,
                purchasesCount = u.PurchasesCount,
                createdAt = u.CreatedAt,
                lastAnalysisAt = u.LastAnalysisAt
            })
        });
    }

    public async Task<IActionResult> ProcessKiwifyWebhook(
        AdminProcessKiwifyWebhookSignature body,
        CancellationToken cancellationToken)
    {
        if (!await EnsureAdminAsync(cancellationToken)) return AdminDenied();

        var rawBody = body.Payload?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(rawBody))
        {
            return BadRequest(new { success = false, error = "Informe o JSON do webhook Kiwify no campo payload." });
        }

        KiwifyWebhookSignature? payload;
        try
        {
            payload = TryParseKiwifyWebhookPayload(rawBody);
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = "JSON inválido.", message = ex.Message });
        }

        if (payload?.Order == null)
        {
            return BadRequest(new { success = false, error = "JSON sem objeto order (formato de webhook Kiwify)." });
        }

        var orderId = payload.Order.OrderRef ?? payload.Order.OrderId;
        var orderRef = payload.Order.OrderRef;
        var eventType = payload.Order.WebhookEventType;
        var paymentStatus = payload.Order.OrderStatus;

        try
        {
            var handleResult = await _kiwify.HandleWebhookAsync(payload, rawBody, cancellationToken);
            var result = handleResult.Verification;
            var processed = result?.Paid == true && result.AlreadyFulfilled != true;
            var alreadyFulfilled = result?.AlreadyFulfilled == true;

            if (result?.Paid == true && result.User != null)
            {
                var paymentId = orderRef ?? orderId ?? string.Empty;
                var externalRef = payload.Order.TrackingParameters?.Sck;
                var meta = TryParseExternalReference(externalRef);
                var planId = meta?.P ?? "single";
                await _data.MarkPendingPurchasesSubstitutedAsync(result.User.Id, planId, cancellationToken);

                if (!string.IsNullOrWhiteSpace(body.PendingPurchaseId))
                {
                    await _data.UpdatePurchaseStatusAsync(
                        body.PendingPurchaseId.Trim(),
                        "substituida",
                        paymentId,
                        cancellationToken);
                }
            }

            KiwifySaleDetailsDto? sale = null;
            if (!string.IsNullOrWhiteSpace(orderId))
            {
                try
                {
                    sale = await _kiwify.GetSaleDetailsAsync(orderId, cancellationToken);
                }
                catch
                {
                    // detalhes opcionais para o painel
                }
            }

            var message = processed
                ? "Créditos liberados via JSON do webhook."
                : alreadyFulfilled
                    ? "Esta venda já havia sido baixada no sistema."
                    : handleResult.FailureMessage
                      ?? (result?.Paid == true ? "Processado." : "Pagamento não aprovado ou evento ignorado.");

            return Ok(new
            {
                success = true,
                processed,
                paid = result?.Paid == true,
                alreadyFulfilled,
                credits = result?.User?.Credits,
                userId = result?.User?.Id,
                message,
                orderId = payload.Order.OrderId,
                orderRef,
                eventType,
                paymentStatus,
                failureStage = handleResult.FailureStage,
                failureMessage = handleResult.FailureMessage,
                sale
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, error = "Erro ao processar JSON Kiwify", message = ex.Message });
        }
    }

    private async Task<UserProfile?> ResolveTargetUserAsync(
        string? userId,
        string? email,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(userId))
        {
            return await _data.GetUserProfileAsync(userId.Trim(), cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            return await _data.GetUserProfileByEmailAsync(email.Trim(), cancellationToken: cancellationToken);
        }

        return null;
    }

    private static KiwifyExternalReferenceLite? TryParseExternalReference(string? externalReference)
    {
        if (string.IsNullOrWhiteSpace(externalReference))
        {
            return null;
        }

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<KiwifyExternalReferenceLite>(externalReference);
        }
        catch
        {
            return null;
        }
    }

    private static KiwifyWebhookSignature? TryParseKiwifyWebhookPayload(string rawBody)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        using var doc = JsonDocument.Parse(rawBody);
        var root = doc.RootElement;

        if (root.TryGetProperty("order", out var orderElement) && orderElement.ValueKind == JsonValueKind.Object)
        {
            return JsonSerializer.Deserialize<KiwifyWebhookSignature>(rawBody, options);
        }

        if (root.TryGetProperty("order_id", out _) ||
            root.TryGetProperty("order_ref", out _) ||
            root.TryGetProperty("webhook_event_type", out _))
        {
            var order = JsonSerializer.Deserialize<KiwifyWebhookOrderSignature>(rawBody, options);
            return order == null ? null : new KiwifyWebhookSignature { Order = order };
        }

        return JsonSerializer.Deserialize<KiwifyWebhookSignature>(rawBody, options);
    }

    private IReadOnlyList<object> BuildKiwifyProductMap(PricingConfigDto config)
    {
        return
        [
            BuildKiwifyProductRow("Análise única", "single", false, config.SinglePriceBRL, config),
            BuildKiwifyProductRow("Análise única + Inglês", "single", true, config.SinglePriceBRL + config.EnglishBundlePriceBRL, config),
            BuildKiwifyProductRow("Pacote 3", "pack3", false, config.Pack3PriceBRL, config),
            BuildKiwifyProductRow("Pacote 3 + Inglês", "pack3", true, config.Pack3PriceBRL + config.EnglishBundlePriceBRL, config),
            BuildKiwifyProductRow("Pacote 5", "pack5", false, config.Pack5PriceBRL, config),
            BuildKiwifyProductRow("Pacote 5 + Inglês", "pack5", true, config.Pack5PriceBRL + config.EnglishBundlePriceBRL, config),
            BuildKiwifyProductRow("Currículo em Inglês (avulso)", "english", false, config.EnglishPriceBRL, config)
        ];
    }

    private object BuildKiwifyProductRow(
        string label,
        string planId,
        bool includeEnglish,
        decimal basePriceBRL,
        PricingConfigDto config)
    {
        var envKey = KiwifyConfigHelper.GetCheckoutEnvKey(planId, includeEnglish);

        var checkoutCode = KiwifyConfigHelper.GetCheckoutCode(_configuration, planId, includeEnglish);

        return new
        {
            label,
            envKey,
            planId,
            includeEnglish,
            basePriceBRL = Math.Round(basePriceBRL, 2),
            displayPriceBRL = config.GetDisplayPrice(basePriceBRL),
            transactionFeeBRL = config.TransactionFeeBRL,
            checkoutCode,
            configured = !string.IsNullOrWhiteSpace(checkoutCode)
        };
    }

    private sealed class KiwifyExternalReferenceLite
    {
        public string? P { get; set; }
    }

}
