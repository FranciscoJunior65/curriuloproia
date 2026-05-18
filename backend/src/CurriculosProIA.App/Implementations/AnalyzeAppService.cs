using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using CurriculosProIA.Repository.Interfaces;
using CurriculosProIA.Repository.Persistence;
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
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;



using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CurriculosProIA.App.Implementations;
using CurriculosProIA.App;
using CurriculosProIA.App.Interfaces;

public class AnalyzeAppService : AppControllerBase, IAnalyzeAppService 
{
    private readonly IHttpContextAccessor _http;
    private readonly IFileService _fileService;
    private readonly IAiService _aiService;
    private readonly IAppDataStore _data;
    private readonly IPricingService _pricing;
    private readonly ISettingsService _settings;
    private readonly IPaymentProviderService _paymentProvider;
    private readonly IPaymentFulfillmentService _fulfillment;
    private readonly IResumeGeneratorService _resumeGenerator;
    private readonly ICoverLetterService _coverLetter;
    private readonly IJobSearchService _jobSearch;
    private readonly IInterviewSimulationService _interviewSimulation;
    private readonly IAnalysisRepository _analysis;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AnalyzeAppService> _logger;

    public AnalyzeAppService(
        IFileService fileService,
        IAiService aiService,
        IAppDataStore data,
        IPricingService pricing,
        ISettingsService settings,
        IPaymentProviderService paymentProvider,
        IPaymentFulfillmentService fulfillment,
        IResumeGeneratorService resumeGenerator,
        ICoverLetterService coverLetter,
        IJobSearchService jobSearch,
        IInterviewSimulationService interviewSimulation,
        IAnalysisRepository analysis,
        IConfiguration configuration,
        ILogger<AnalyzeAppService> logger,
        IHttpContextAccessor http)
    {
        _fileService = fileService;
        _aiService = aiService;
        _data = data;
        _pricing = pricing;
        _settings = settings;
        _paymentProvider = paymentProvider;
        _fulfillment = fulfillment;
        _resumeGenerator = resumeGenerator;
        _coverLetter = coverLetter;
        _jobSearch = jobSearch;
        _interviewSimulation = interviewSimulation;
        _analysis = analysis;
        _configuration = configuration;
        _logger = logger;
        _http = http;
    }
        public async Task<IActionResult> Upload(
        IFormFile? file,
        string? userId,
        string? siteId,
        string? curriculoId,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        var resolvedUserId = userId ?? JwtAuthHelper.TryGetUserId(_http.HttpContext!.Request.Headers, _configuration);

        if (string.IsNullOrEmpty(resolvedUserId))
        {
            return Unauthorized(new
            {
                success = false,
                error = "Não autenticado",
                message = "É necessário estar autenticado para analisar currículos.",
                requiresAuth = true
            });
        }

        var user = await _data.GetUserProfileAsync(resolvedUserId, cancellationToken);
        if (user == null)
        {
            return NotFound(new { success = false, error = "Usuário não encontrado", message = "Usuário não encontrado no sistema." });
        }

        if (!await _data.UserHasCreditsAsync(resolvedUserId, 1, cancellationToken))
        {
            return StatusCode(402, new
            {
                success = false,
                error = "Créditos insuficientes",
                message = "Você não possui créditos suficientes. Por favor, adquira um plano.",
                requiresPayment = true,
                creditsAvailable = user.Credits
            });
        }

        if (file == null || file.Length == 0)
        {
            return BadRequest(new
            {
                success = false,
                error = "Nenhum arquivo enviado",
                message = "Por favor, envie um arquivo de currículo (PDF, DOC, DOCX ou TXT)"
            });
        }

        if (file.Length > 10 * 1024 * 1024)
        {
            return BadRequest(new
            {
                success = false,
                error = "Arquivo muito grande",
                message = "O arquivo excede o tamanho máximo de 10MB"
            });
        }

        string text;
        try
        {
            text = await _fileService.ExtractTextFromFileAsync(file, cancellationToken);
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                success = false,
                error = "Erro ao extrair texto",
                message = ex.Message
            });
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return BadRequest(new
            {
                success = false,
                error = "Texto vazio",
                message = "Não foi possível extrair texto do arquivo. O arquivo pode estar vazio ou corrompido."
            });
        }

        ResumeAnalysisResult analysis;
        try
        {
            analysis = await _aiService.AnalyzeResumeAsync(text, resolvedUserId, curriculoId, siteId, cancellationToken);
        }
        catch (Exception ex)
        {
            var processingTimeErr = (DateTime.UtcNow - startTime).TotalSeconds;
            _logger.LogError(ex, "Erro ao analisar currículo ({Seconds}s)", processingTimeErr);
            var statusCode = ex.Message.Contains("429", StringComparison.Ordinal) ? 429 : 500;
            return StatusCode(statusCode, new
            {
                success = false,
                error = "Erro ao processar currículo",
                message = ex.Message
            });
        }

        await _data.RecordCreditUsageAsync(
            resolvedUserId,
            "analysis",
            1,
            file.FileName,
            siteId,
            cancellationToken);

        var creditsRemaining = await _data.GetAvailableCreditsAsync(resolvedUserId, cancellationToken);
        var processingTime = (DateTime.UtcNow - startTime).TotalSeconds;

        return Ok(new
        {
            success = true,
            originalText = text,
            analysis,
            resumeId = (string?)null,
            metadata = new
            {
                fileName = file.FileName,
                fileSize = file.Length,
                textLength = text.Length,
                processingTime = $"{processingTime:F2}s"
            },
            creditsRemaining
        });
    }

        public async Task<IActionResult> GenerateImproved(
        GenerateImprovedSignature body,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            if (string.IsNullOrWhiteSpace(body.OriginalText) || body.Analysis == null)
            {
                return BadRequest(new
                {
                    success = false,
                    error = "Dados incompletos",
                    message = "É necessário fornecer originalText e analysis"
                });
            }

            if (body.Analysis.PontosFortes == null || body.Analysis.Recomendacoes == null)
            {
                return BadRequest(new
                {
                    success = false,
                    error = "Análise inválida",
                    message = "A análise deve conter pontosFortes e recomendacoes"
                });
            }

            var improvedResume = await _resumeGenerator.GenerateImprovedResumeAsync(
                body.OriginalText,
                body.Analysis,
                body.SiteId,
                cancellationToken);

            var pdfBuffer = _resumeGenerator.GenerateResumePdf(improvedResume);
            var processingTime = (DateTime.UtcNow - startTime).TotalSeconds;
            _logger.LogInformation("Currículo melhorado gerado em {Seconds:F2}s", processingTime);

            return File(pdfBuffer, "application/pdf", "curriculo-melhorado.pdf");
        }
        catch (Exception ex)
        {
            var processingTime = (DateTime.UtcNow - startTime).TotalSeconds;
            _logger.LogError(ex, "Erro ao gerar currículo melhorado ({Seconds:F2}s)", processingTime);
            return StatusCode(500, new
            {
                success = false,
                error = "Erro ao gerar currículo melhorado",
                message = ex.Message
            });
        }
    }

        public async Task<IActionResult> GenerateCoverLetter(
        GenerateCoverLetterSignature body,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            if (string.IsNullOrWhiteSpace(body.ResumeText) || body.Analysis == null)
            {
                return BadRequest(new
                {
                    success = false,
                    error = "Dados incompletos",
                    message = "É necessário fornecer resumeText e analysis"
                });
            }

            if (body.Analysis.PontosFortes == null || string.IsNullOrWhiteSpace(body.Analysis.Experiencia))
            {
                return BadRequest(new
                {
                    success = false,
                    error = "Análise inválida",
                    message = "A análise deve conter pontosFortes e experiencia"
                });
            }

            var coverLetterText = await _coverLetter.GenerateCoverLetterAsync(
                body.ResumeText,
                body.Analysis,
                body.SiteId,
                cancellationToken);

            var pdfBuffer = _coverLetter.GenerateCoverLetterPdf(coverLetterText);
            var fileName = await BuildCoverLetterFileNameAsync(cancellationToken);

            var processingTime = (DateTime.UtcNow - startTime).TotalSeconds;
            _logger.LogInformation("Carta de apresentação gerada em {Seconds:F2}s", processingTime);

            return File(pdfBuffer, "application/pdf", fileName);
        }
        catch (Exception ex)
        {
            var processingTime = (DateTime.UtcNow - startTime).TotalSeconds;
            _logger.LogError(ex, "Erro ao gerar carta de apresentação ({Seconds:F2}s)", processingTime);
            return StatusCode(500, new
            {
                success = false,
                error = "Erro ao gerar carta de apresentação",
                message = ex.Message
            });
        }
    }

        public async Task<IActionResult> SearchJobs(
        SearchJobsSignature body,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            var userId = JwtAuthHelper.TryGetUserId(_http.HttpContext!.Request.Headers, _configuration);

            if (body.Analysis == null || string.IsNullOrWhiteSpace(body.SiteId))
            {
                return BadRequest(new
                {
                    success = false,
                    error = "Dados incompletos",
                    message = "É necessário fornecer analysis e siteId"
                });
            }

            if ((body.Analysis.Habilidades == null || body.Analysis.Habilidades.Count == 0) &&
                string.IsNullOrWhiteSpace(body.Analysis.Experiencia))
            {
                return BadRequest(new
                {
                    success = false,
                    error = "Análise inválida",
                    message = "A análise deve conter habilidades ou experiencia para buscar vagas"
                });
            }

            var results = await _jobSearch.SearchJobsBySiteAsync(
                body.SiteId,
                body.Analysis,
                body.Location ?? "Brasil",
                body.ResumeText,
                userId,
                body.ResumeId,
                cancellationToken);

            var processingTime = (DateTime.UtcNow - startTime).TotalSeconds.ToString("F2", CultureInfo.InvariantCulture) + "s";

            return Ok(new
            {
                success = true,
                site = results.Site,
                url = results.Url,
                jobs = results.Jobs,
                message = results.Message,
                searchTerms = results.SearchTerms,
                totalFound = results.TotalFound,
                searchKeywords = results.SearchKeywords,
                searchCombinations = results.SearchCombinations,
                processingTime
            });
        }
        catch (Exception ex)
        {
            var processingTime = (DateTime.UtcNow - startTime).TotalSeconds;
            _logger.LogError(ex, "Erro ao buscar vagas ({Seconds:F2}s)", processingTime);
            return StatusCode(500, new
            {
                success = false,
                error = "Erro ao buscar vagas",
                message = ex.Message
            });
        }
    }

        public async Task<IActionResult> StartInterview(
        StartInterviewSignature body,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(body.ResumeText) || body.Analysis == null)
            {
                return BadRequest(new
                {
                    success = false,
                    error = "Dados incompletos",
                    message = "É necessário fornecer resumeText e analysis"
                });
            }

            var userId = JwtAuthHelper.TryGetUserId(_http.HttpContext!.Request.Headers, _configuration);
            var (simulationId, questions) = await _interviewSimulation.StartInterviewAsync(
                body.ResumeText,
                body.Analysis,
                body.SiteId,
                userId,
                body.ResumeId,
                cancellationToken);

            return Ok(new
            {
                success = true,
                simulationId,
                questions,
                message = $"{questions.Count} perguntas geradas"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao iniciar entrevista");
            return StatusCode(500, new
            {
                success = false,
                error = "Erro ao iniciar entrevista",
                message = ex.Message
            });
        }
    }

        public async Task<IActionResult> EvaluateInterview(
        EvaluateInterviewSignature body,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(body.Question) ||
                string.IsNullOrWhiteSpace(body.Answer) ||
                string.IsNullOrWhiteSpace(body.ResumeText) ||
                body.Analysis == null)
            {
                return BadRequest(new
                {
                    success = false,
                    error = "Dados incompletos",
                    message = "É necessário fornecer question, answer, resumeText e analysis"
                });
            }

            var evaluation = await _interviewSimulation.EvaluateAnswerAsync(
                body.Question,
                body.Answer,
                body.ResumeText,
                body.Analysis,
                cancellationToken);

            if (!string.IsNullOrEmpty(body.SimulationId))
            {
                try
                {
                    var interview = await _interviewSimulation.GetInterviewByIdAsync(body.SimulationId, cancellationToken);
                    var existingQuestions = interview?.Messages.Count(m => m.Tipo == "pergunta") ?? 0;
                    var questionOrder = existingQuestions > 0 ? existingQuestions : 1;
                    await _interviewSimulation.SaveInterviewMessageAsync(
                        body.SimulationId,
                        body.Question,
                        body.Answer,
                        evaluation,
                        questionOrder,
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Erro ao salvar mensagens da entrevista");
                }
            }

            return Ok(new
            {
                success = true,
                evaluation
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao avaliar resposta");
            return StatusCode(500, new
            {
                success = false,
                error = "Erro ao avaliar resposta",
                message = ex.Message
            });
        }
    }

        public async Task<IActionResult> FinishInterview(
        FinishInterviewSignature body,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(body.SimulationId))
            {
                return BadRequest(new
                {
                    success = false,
                    error = "Dados incompletos",
                    message = "É necessário fornecer simulationId"
                });
            }

            var averageScore = await _interviewSimulation.FinishInterviewAsync(
                body.SimulationId,
                body.AllAnswers ?? new List<InterviewAnswerItem>(),
                cancellationToken);

            return Ok(new
            {
                success = true,
                score = averageScore,
                simulationId = body.SimulationId,
                message = "Simulação finalizada com sucesso"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao finalizar simulação");
            return StatusCode(500, new
            {
                success = false,
                error = "Erro ao finalizar simulação",
                message = ex.Message
            });
        }
    }

        public async Task<IActionResult> ListUserInterviews(CancellationToken cancellationToken)
    {
        try
        {
            var userId = JwtAuthHelper.TryGetUserId(_http.HttpContext!.Request.Headers, _configuration);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new
                {
                    success = false,
                    error = "Não autenticado",
                    message = "É necessário estar autenticado"
                });
            }

            var interviews = await _interviewSimulation.GetUserInterviewsAsync(userId, cancellationToken);
            return Ok(new
            {
                success = true,
                interviews = interviews ?? new List<SimulacaoEntrevistaRow>()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar entrevistas");
            return StatusCode(500, new
            {
                success = false,
                error = "Erro ao listar entrevistas",
                message = ex.Message
            });
        }
    }

        public async Task<IActionResult> GetInterview(string simulationId, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(simulationId))
            {
                return BadRequest(new
                {
                    success = false,
                    error = "Dados incompletos",
                    message = "É necessário fornecer simulationId"
                });
            }

            var interview = await _interviewSimulation.GetInterviewByIdAsync(simulationId, cancellationToken);
            if (interview == null)
            {
                return NotFound(new
                {
                    success = false,
                    error = "Entrevista não encontrada",
                    message = "Simulação não encontrada"
                });
            }

            var userId = JwtAuthHelper.TryGetUserId(_http.HttpContext!.Request.Headers, _configuration);
            if (!string.IsNullOrEmpty(userId) && interview.IdUsuario != userId)
            {
                return StatusCode(403, new
                {
                    success = false,
                    error = "Acesso negado",
                    message = "Você não tem permissão para acessar esta entrevista"
                });
            }

            return Ok(new
            {
                success = true,
                interview
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar entrevista");
            return StatusCode(500, new
            {
                success = false,
                error = "Erro ao buscar entrevista",
                message = ex.Message
            });
        }
    }

        public async Task<IActionResult> DownloadInterview(string simulationId, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(simulationId))
            {
                return BadRequest(new
                {
                    success = false,
                    error = "Dados incompletos",
                    message = "É necessário fornecer simulationId"
                });
            }

            var interview = await _interviewSimulation.GetInterviewByIdAsync(simulationId, cancellationToken);
            if (interview == null)
            {
                return NotFound(new
                {
                    success = false,
                    error = "Entrevista não encontrada",
                    message = "Simulação não encontrada"
                });
            }

            var userId = JwtAuthHelper.TryGetUserId(_http.HttpContext!.Request.Headers, _configuration);
            if (!string.IsNullOrEmpty(userId) && interview.IdUsuario != userId)
            {
                return StatusCode(403, new
                {
                    success = false,
                    error = "Acesso negado",
                    message = "Você não tem permissão para acessar esta entrevista"
                });
            }

            var content = _interviewSimulation.BuildInterviewDownloadContent(interview);
            var fileName = $"entrevista_{interview.Id}_{DateTime.UtcNow:yyyy-MM-dd}.txt";
            var bytes = Encoding.UTF8.GetBytes(content);
            return File(bytes, "text/plain; charset=utf-8", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao gerar download da entrevista");
            return StatusCode(500, new
            {
                success = false,
                error = "Erro ao gerar download",
                message = ex.Message
            });
        }
    }

        public IActionResult GetPlans()
    {
        var plans = _pricing.PricingPlans.Values.Select(plan =>
        {
            var margin = _pricing.CalculateProfitMargin(plan.Id);
            return new
            {
                plan.Id,
                plan.Name,
                plan.Description,
                plan.Analyses,
                plan.PriceBRL,
                plan.PriceUSD,
                plan.Savings,
                plan.PriceBRLBundle,
                plan.Features,
                profitMargin = margin
            };
        });

        return Ok(new { success = true, plans });
    }

        public async Task<IActionResult> GetActivePaymentProvider(CancellationToken cancellationToken)
    {
        try
        {
            var provider = await _settings.GetPaymentProviderAsync(cancellationToken);
            return Ok(new
            {
                success = true,
                provider,
                providers = _settings.GetValidPaymentProviders()
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, error = "Erro ao obter provedor de pagamento", message = ex.Message });
        }
    }

            public async Task<IActionResult> ValidateCoupon(
        string? code,
        string? cpf,
        CouponValidateSignature? body,
        CancellationToken cancellationToken)
    {
        var couponCode = code ?? body?.Code;
        var couponCpf = cpf ?? body?.Cpf;

        if (string.IsNullOrWhiteSpace(couponCode))
        {
            return BadRequest(new { success = false, valid = false, error = "Código do cupom é obrigatório" });
        }

        try
        {
            var result = await _data.ValidateCouponAsync(couponCode.Trim(), couponCpf?.Trim(), cancellationToken);
            if (!result.Valid)
            {
                return Ok(new { success = true, valid = false, message = result.Message ?? "Cupom inválido ou inativo." });
            }

            if (string.IsNullOrWhiteSpace(couponCpf))
            {
                return Ok(new
                {
                    success = true,
                    valid = true,
                    coupon = new
                    {
                        nome = result.Coupon?.Nome,
                        porcentagem_desconto = result.Coupon?.PorcentagemDesconto
                    },
                    message = "Informe seu CPF antes de finalizar a compra para usar este cupom."
                });
            }

            return Ok(new
            {
                success = true,
                valid = true,
                coupon = new
                {
                    nome = result.Coupon?.Nome,
                    porcentagem_desconto = result.Coupon?.PorcentagemDesconto
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, valid = false, error = "Erro ao validar cupom", message = ex.Message });
        }
    }

        public async Task<IActionResult> CreatePaymentSession(CreatePaymentSessionSignature body, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrEmpty(body.PlanId) || _pricing.GetPlan(body.PlanId) == null)
            {
                return BadRequest(new { success = false, error = "Plano inválido" });
            }

            var userId = body.UserId ?? JwtAuthHelper.TryGetUserId(_http.HttpContext!.Request.Headers, _configuration);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { success = false, error = "É necessário estar autenticado para realizar a compra" });
            }

            var frontendUrl = _http.HttpContext!.Request.Headers.Origin.FirstOrDefault()
                ?? _configuration["FRONTEND_URL"];
            var couponCode = string.IsNullOrWhiteSpace(body.CouponCode) ? null : body.CouponCode.Trim();
            var cpf = body.Cpf?.Trim();

            var result = await _paymentProvider.CreateProviderCheckoutAsync(
                body.PlanId,
                userId,
                body.Email ?? string.Empty,
                frontendUrl,
                couponCode,
                cpf,
                cancellationToken);

            if (result.FreeCheckout)
            {
                var fulfillment = await _fulfillment.FulfillFreeCheckoutAsync(new FulfillOrderRequest
                {
                    UserId = userId,
                    PlanId = result.PlanId ?? body.PlanId,
                    PlanName = result.PlanName ?? string.Empty,
                    Analyses = result.Analyses,
                    CouponId = result.CouponId,
                    CouponName = result.CouponName,
                    DiscountPercent = result.DiscountPercent,
                    OriginalPrice = result.OriginalPrice,
                    CpfNormalized = result.CpfNormalized
                }, cancellationToken);

                var baseUrl = (frontendUrl ?? _configuration["FRONTEND_URL"] ?? string.Empty).TrimEnd('/');
                return Ok(new
                {
                    success = true,
                    freeCheckout = true,
                    provider = result.Provider,
                    redirectUrl = $"{baseUrl}?free=1&userId={userId}",
                    user = fulfillment.User
                });
            }

            return Ok(new
            {
                success = true,
                provider = result.Provider,
                sessionId = result.SessionId,
                checkoutUrl = result.Url,
                preferenceId = result.PreferenceId
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, error = "Erro ao criar sessão de pagamento", message = ex.Message });
        }
    }

        public async Task<IActionResult> AdminFreeCredits(AdminFreeCreditsSignature body, CancellationToken cancellationToken)
    {
        var userId = JwtAuthHelper.TryGetUserId(_http.HttpContext!.Request.Headers, _configuration);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new { success = false, error = "Token não fornecido" });
        }

        var profile = await _data.GetUserProfileAsync(userId, cancellationToken);
        if (profile?.UserType != "admin")
        {
            return StatusCode(403, new { success = false, error = "Acesso negado" });
        }

        var plan = _pricing.GetPlan(body.PlanId ?? string.Empty);
        if (plan == null)
        {
            return BadRequest(new { success = false, error = "planId inválido. Use: single, pack3 ou pack5." });
        }

        await _data.CreatePurchaseAsync(
            userId,
            plan.Id,
            plan.Name,
            plan.Analyses,
            0,
            paymentMethod: "admin_test",
            paymentId: $"admin_free_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{userId}",
            cancellationToken: cancellationToken);

        var credits = await _data.GetAvailableCreditsAsync(userId, cancellationToken);
        return Ok(new
        {
            success = true,
            message = $"{plan.Analyses} crédito(s) adicionado(s) para testes.",
            credits
        });
    }

        public async Task<IActionResult> VerifyPayment(
        string? sessionId,
        string? payment_id,
        string? paymentId,
        string? provider,
        CancellationToken cancellationToken)
    {
        try
        {
            var id = sessionId ?? payment_id ?? paymentId;
            if (string.IsNullOrEmpty(id))
            {
                return BadRequest(new { success = false, error = "sessionId ou payment_id é obrigatório" });
            }

            var result = await _paymentProvider.VerifyProviderPaymentAsync(id, provider, cancellationToken);
            if (result.Paid)
            {
                var activeProvider = provider ?? await _settings.GetPaymentProviderAsync(cancellationToken);
                return Ok(new
                {
                    success = true,
                    paid = true,
                    provider = activeProvider,
                    user = result.User,
                    alreadyFulfilled = result.AlreadyFulfilled
                });
            }

            return Ok(new
            {
                success = true,
                paid = false,
                paymentStatus = result.PaymentStatus,
                statusDetail = result.StatusDetail
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, error = "Erro ao verificar pagamento", message = ex.Message });
        }
    }

        public async Task<IActionResult> GetCredits(string? userId, CancellationToken cancellationToken)
    {
        var resolvedUserId = userId ?? JwtAuthHelper.TryGetUserId(_http.HttpContext!.Request.Headers, _configuration);
        if (string.IsNullOrEmpty(resolvedUserId))
        {
            return Unauthorized(new { success = false, error = "Não autenticado" });
        }

        var user = await _data.GetUserProfileAsync(resolvedUserId, cancellationToken);
        if (user == null)
        {
            return NotFound(new { success = false, error = "Usuário não encontrado" });
        }

        return Ok(new
        {
            success = true,
            credits = user.Credits,
            plan = user.Plan,
            lastAnalysis = user.LastAnalysis
        });
    }

        public async Task<IActionResult> ListJobSites(CancellationToken cancellationToken)
    {
        try
        {
            var sites = await _data.GetActiveJobSitesAsync(cancellationToken);
            return Ok(new { success = true, sites });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, error = "Erro ao listar sites de vagas", message = ex.Message });
        }
    }

        public async Task<IActionResult> ListUserAnalyses(
        int limit = 50,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = JwtAuthHelper.TryGetUserId(_http.HttpContext!.Request.Headers, _configuration);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new
                {
                    success = false,
                    error = "Não autenticado",
                    message = "É necessário estar autenticado para listar análises"
                });
            }

            var analyses = await _analysis.GetUserAnalysesAsync(userId, limit, offset, cancellationToken);
            return Ok(new
            {
                success = true,
                analyses,
                total = analyses.Count,
                limit,
                offset
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar análises");
            return StatusCode(500, new
            {
                success = false,
                error = "Erro ao listar análises",
                message = ex.Message
            });
        }
    }

        public async Task<IActionResult> GetAnalysis(string analysisId, CancellationToken cancellationToken)
    {
        try
        {
            var userId = JwtAuthHelper.TryGetUserId(_http.HttpContext!.Request.Headers, _configuration);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new
                {
                    success = false,
                    error = "Não autenticado",
                    message = "É necessário estar autenticado para buscar análise"
                });
            }

            if (string.IsNullOrWhiteSpace(analysisId))
            {
                return BadRequest(new
                {
                    success = false,
                    error = "ID não fornecido",
                    message = "É necessário fornecer o ID da análise"
                });
            }

            var analysis = await _analysis.GetAnalysisByIdAsync(analysisId, cancellationToken);
            if (analysis == null)
            {
                return NotFound(new
                {
                    success = false,
                    error = "Análise não encontrada",
                    message = "Análise não encontrada"
                });
            }

            if (analysis.IdUsuario != userId)
            {
                return StatusCode(403, new
                {
                    success = false,
                    error = "Acesso negado",
                    message = "Você não tem permissão para acessar esta análise"
                });
            }

            return Ok(new
            {
                success = true,
                analysis
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar análise");
            return StatusCode(500, new
            {
                success = false,
                error = "Erro ao buscar análise",
                message = ex.Message
            });
        }
    }

    private async Task<string> BuildCoverLetterFileNameAsync(CancellationToken cancellationToken)
    {
        var userName = "carta-apresentacao";
        var userId = JwtAuthHelper.TryGetUserId(_http.HttpContext!.Request.Headers, _configuration);
        if (string.IsNullOrEmpty(userId))
        {
            return $"{userName}-carta-apresentacao.pdf";
        }

        try
        {
            var profile = await _data.GetUserProfileAsync(userId, cancellationToken);
            var name = profile?.Name;
            if (!string.IsNullOrWhiteSpace(name))
            {
                userName = new string(name.Normalize(NormalizationForm.FormD)
                    .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    .ToArray())
                    .ToLowerInvariant();
                userName = Regex.Replace(userName, @"[^a-z0-9\s]", "");
                userName = Regex.Replace(userName, @"\s+", "-").Trim('-');
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Não foi possível obter nome do usuário para arquivo da carta");
        }

        return $"{userName}-carta-apresentacao.pdf";
    }

}
