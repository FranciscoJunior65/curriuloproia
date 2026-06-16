using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using CurriculosProIA.Repository.Interfaces;
using CurriculosProIA.Repository.Persistence;
using CurriculosProIA.Service.Interfaces;
using CurriculosProIA.Domain.Entities;
using CurriculosProIA.Domain.Dtos;
using CurriculosProIA.Domain.Helpers;
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
    private readonly IMercadoPagoService _mercadoPago;
    private readonly IPaymentFulfillmentService _fulfillment;
    private readonly IResumeGeneratorService _resumeGenerator;
    private readonly ICoverLetterService _coverLetter;
    private readonly IJobSearchService _jobSearch;
    private readonly IInterviewSimulationService _interviewSimulation;
    private readonly IVoiceInterviewService _voiceInterview;
    private readonly IStructuredInterviewService _structuredInterview;
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
        IMercadoPagoService mercadoPago,
        IPaymentFulfillmentService fulfillment,
        IResumeGeneratorService resumeGenerator,
        ICoverLetterService coverLetter,
        IJobSearchService jobSearch,
        IInterviewSimulationService interviewSimulation,
        IVoiceInterviewService voiceInterview,
        IStructuredInterviewService structuredInterview,
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
        _mercadoPago = mercadoPago;
        _fulfillment = fulfillment;
        _resumeGenerator = resumeGenerator;
        _coverLetter = coverLetter;
        _jobSearch = jobSearch;
        _interviewSimulation = interviewSimulation;
        _voiceInterview = voiceInterview;
        _structuredInterview = structuredInterview;
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

        var creditUsage = await _data.RecordCreditUsageAsync(
            resolvedUserId,
            "analysis",
            1,
            file.FileName,
            siteId,
            cancellationToken);

        string? resumeId = null;
        string? analysisId = null;
        if (!string.IsNullOrEmpty(siteId))
        {
            try
            {
                resumeId = await _data.SaveImportedResumeAsync(
                    resolvedUserId,
                    siteId,
                    file.FileName,
                    file.ContentType ?? "application/pdf",
                    text,
                    creditUsage.Id,
                    analysis,
                    cancellationToken);

                if (!string.IsNullOrEmpty(resumeId))
                {
                    analysisId = await _analysis.SaveAnalysisAsync(
                        resumeId,
                        resolvedUserId,
                        siteId,
                        analysis,
                        cancellationToken);

                    if (!string.IsNullOrEmpty(analysisId) && !string.IsNullOrEmpty(creditUsage.Id))
                    {
                        await _analysis.TryGrantBundledEnglishFromCreditAsync(
                            resolvedUserId,
                            creditUsage.Id,
                            analysisId,
                            cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Análise concluída, mas falhou ao persistir histórico (currículo/análise)");
            }
        }

        var creditsRemaining = await _data.GetAvailableCreditsAsync(resolvedUserId, cancellationToken);
        var processingTime = (DateTime.UtcNow - startTime).TotalSeconds;

        AnalysisServicesStatusDto? servicos = null;
        if (!string.IsNullOrEmpty(analysisId))
        {
            servicos = await _analysis.GetServicesStatusAsync(analysisId, cancellationToken);
        }

        return Ok(new
        {
            success = true,
            originalText = text,
            analysis,
            resumeId,
            analysisId,
            servicos,
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
        var format = (body.Format ?? "pdf").Trim().ToLowerInvariant();

        try
        {
            var accessError = await EnsureBundledServiceAccessAsync(body.AnalysisId, cancellationToken);
            if (accessError != null)
            {
                return accessError;
            }

            var (ctx, resolveError) = await ResolveAnalysisContextAsync(
                body.AnalysisId,
                body.Analysis,
                body.OriginalText,
                resumeId: null,
                body.SiteId,
                cancellationToken);
            if (resolveError != null)
            {
                return resolveError;
            }

            if (string.IsNullOrWhiteSpace(ctx!.ResumeText))
            {
                return BadRequest(new
                {
                    success = false,
                    error = "Currículo não encontrado",
                    message = "O texto do currículo não está disponível no histórico. Faça uma nova análise."
                });
            }

            if (ctx.Analysis.PontosFortes == null || ctx.Analysis.PontosFortes.Count == 0)
            {
                return BadRequest(new
                {
                    success = false,
                    error = "Análise inválida",
                    message = "A análise deve conter pontos fortes para gerar o currículo melhorado"
                });
            }

            var improvedResume = await _resumeGenerator.GenerateImprovedResumeAsync(
                ctx.ResumeText!,
                ctx.Analysis,
                ctx.SiteId ?? body.SiteId,
                cancellationToken);

            if (format == "word")
            {
                var docxBuffer = _resumeGenerator.GenerateResumeDocx(improvedResume);
                await TryMarkServiceUsedAsync(body.AnalysisId, AnalysisBundledServiceKeys.CurriculoMelhorado, cancellationToken);

                var wordTime = (DateTime.UtcNow - startTime).TotalSeconds;
                _logger.LogInformation("Currículo melhorado (Word) gerado em {Seconds:F2}s", wordTime);

                return File(
                    docxBuffer,
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    "curriculo-melhorado.docx");
            }

            var pdfBuffer = _resumeGenerator.GenerateResumePdf(improvedResume);
            await TryMarkServiceUsedAsync(body.AnalysisId, AnalysisBundledServiceKeys.CurriculoMelhorado, cancellationToken);

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

        public async Task<IActionResult> GenerateEnglishResume(
        GenerateEnglishExcelSignature body,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        var format = (body.Format ?? "pdf").Trim().ToLowerInvariant();

        try
        {
            var accessError = await EnsureEnglishResumeAccessAsync(body.AnalysisId, cancellationToken);
            if (accessError != null)
            {
                return accessError;
            }

            var (ctx, resolveError) = await ResolveAnalysisContextAsync(
                body.AnalysisId,
                body.Analysis,
                body.OriginalText,
                resumeId: null,
                body.SiteId,
                cancellationToken);
            if (resolveError != null)
            {
                return resolveError;
            }

            if (string.IsNullOrWhiteSpace(ctx!.ResumeText))
            {
                return BadRequest(new
                {
                    success = false,
                    error = "Currículo não encontrado",
                    message = "O texto do currículo não está disponível no histórico. Faça uma nova análise."
                });
            }

            var englishResume = await _resumeGenerator.GenerateEnglishResumeAsync(
                ctx.ResumeText!,
                ctx.Analysis,
                ctx.SiteId ?? body.SiteId,
                cancellationToken);

            var formatKey = format == "word"
                ? AnalysisBundledServiceKeys.CurriculoInglesWord
                : AnalysisBundledServiceKeys.CurriculoInglesPdf;

            if (format == "word")
            {
                var docxBuffer = _resumeGenerator.GenerateResumeDocx(englishResume);
                await TryMarkEnglishFormatUsedAsync(body.AnalysisId, formatKey, cancellationToken);
                return File(
                    docxBuffer,
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    "curriculo-ingles.docx");
            }

            var pdfBuffer = _resumeGenerator.GenerateResumePdf(englishResume);
            await TryMarkEnglishFormatUsedAsync(body.AnalysisId, formatKey, cancellationToken);

            var processingTime = (DateTime.UtcNow - startTime).TotalSeconds;
            _logger.LogInformation("Currículo em inglês (PDF) gerado em {Seconds:F2}s", processingTime);

            return File(pdfBuffer, "application/pdf", "curriculo-ingles.pdf");
        }
        catch (Exception ex)
        {
            var processingTime = (DateTime.UtcNow - startTime).TotalSeconds;
            _logger.LogError(ex, "Erro ao gerar currículo em inglês ({Seconds:F2}s)", processingTime);
            return StatusCode(500, new
            {
                success = false,
                error = "Erro ao gerar currículo em inglês",
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
            var accessError = await EnsureBundledServiceAccessAsync(body.AnalysisId, cancellationToken);
            if (accessError != null)
            {
                return accessError;
            }

            var (ctx, resolveError) = await ResolveAnalysisContextAsync(
                body.AnalysisId,
                body.Analysis,
                body.ResumeText,
                resumeId: null,
                body.SiteId,
                cancellationToken);
            if (resolveError != null)
            {
                return resolveError;
            }

            if (string.IsNullOrWhiteSpace(ctx!.ResumeText))
            {
                return BadRequest(new
                {
                    success = false,
                    error = "Currículo não encontrado",
                    message = "O texto do currículo não está disponível no histórico. Faça uma nova análise."
                });
            }

            if (ctx.Analysis.PontosFortes == null || ctx.Analysis.PontosFortes.Count == 0)
            {
                return BadRequest(new
                {
                    success = false,
                    error = "Análise inválida",
                    message = "A análise deve conter pontos fortes para gerar a carta"
                });
            }

            var coverLetterText = await _coverLetter.GenerateCoverLetterAsync(
                ctx.ResumeText!,
                ctx.Analysis,
                ctx.SiteId ?? body.SiteId,
                cancellationToken);

            var pdfBuffer = _coverLetter.GenerateCoverLetterPdf(coverLetterText);
            var fileName = await BuildCoverLetterFileNameAsync(cancellationToken);

            await TryMarkServiceUsedAsync(body.AnalysisId, AnalysisBundledServiceKeys.CartaApresentacao, cancellationToken);

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
            if (string.IsNullOrWhiteSpace(body.AnalysisId))
            {
                return BadRequest(new
                {
                    success = false,
                    error = "Análise obrigatória",
                    message =
                        "A busca de vagas está incluída no currículo analisado. Importe e analise um currículo (ou abra pelo histórico) para pesquisar. Um novo currículo requer novo crédito."
                });
            }

            var accessError = await EnsureBundledServiceAccessAsync(body.AnalysisId, cancellationToken);
            if (accessError != null)
            {
                return accessError;
            }

            var (ctx, resolveError) = await ResolveAnalysisContextAsync(
                body.AnalysisId,
                body.Analysis,
                body.ResumeText,
                body.ResumeId,
                body.SiteId,
                cancellationToken);
            if (resolveError != null)
            {
                return resolveError;
            }

            var siteId = ctx!.SiteId ?? body.SiteId;
            if (string.IsNullOrWhiteSpace(siteId))
            {
                return BadRequest(new
                {
                    success = false,
                    error = "Dados incompletos",
                    message = "É necessário informar o site de vagas"
                });
            }

            if ((ctx.Analysis.Habilidades == null || ctx.Analysis.Habilidades.Count == 0) &&
                string.IsNullOrWhiteSpace(ctx.Analysis.Experiencia) &&
                string.IsNullOrWhiteSpace(ctx.ResumeText))
            {
                return BadRequest(new
                {
                    success = false,
                    error = "Análise inválida",
                    message = "Não há dados suficientes do currículo para buscar vagas. Faça uma nova análise."
                });
            }

            var results = await _jobSearch.SearchJobsBySiteAsync(
                siteId,
                ctx.Analysis,
                body.Location ?? "Brasil",
                ctx.ResumeText,
                userId,
                ctx.ResumeId ?? body.ResumeId,
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
            var accessError = await EnsureBundledServiceAccessAsync(body.AnalysisId, cancellationToken);
            if (accessError != null)
            {
                return accessError;
            }

            var (ctx, resolveError) = await ResolveAnalysisContextAsync(
                body.AnalysisId,
                body.Analysis,
                body.ResumeText,
                body.ResumeId,
                body.SiteId,
                cancellationToken);
            if (resolveError != null)
            {
                return resolveError;
            }

            if (string.IsNullOrWhiteSpace(ctx!.ResumeText))
            {
                return BadRequest(new
                {
                    success = false,
                    error = "Currículo não encontrado",
                    message = "O texto do currículo não está disponível no histórico. Faça uma nova análise."
                });
            }

            var userId = JwtAuthHelper.TryGetUserId(_http.HttpContext!.Request.Headers, _configuration);
            var (simulationId, questions) = await _interviewSimulation.StartInterviewAsync(
                ctx.ResumeText!,
                ctx.Analysis,
                ctx.SiteId ?? body.SiteId,
                userId,
                ctx.ResumeId ?? body.ResumeId,
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

            var analysisIdForInterview = await ResolveAnalysisIdForInterviewAsync(body.AnalysisId, body.SimulationId, cancellationToken);
            await TryMarkServiceUsedAsync(analysisIdForInterview, AnalysisBundledServiceKeys.Entrevista, cancellationToken);

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

        public async Task<IActionResult> DownloadInterview(string simulationId, string? format, CancellationToken cancellationToken)
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

            var normalizedFormat = (format ?? "txt").Trim().ToLowerInvariant();
            var dateSuffix = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var baseName = $"entrevista_{interview.Id}_{dateSuffix}";

            return normalizedFormat switch
            {
                "pdf" => File(
                    _interviewSimulation.GenerateInterviewPdf(interview),
                    "application/pdf",
                    $"{baseName}.pdf"),
                "docx" or "word" => File(
                    _interviewSimulation.GenerateInterviewDocx(interview),
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    $"{baseName}.docx"),
                "txt" or "text" => File(
                    Encoding.UTF8.GetBytes(_interviewSimulation.BuildInterviewDownloadContent(interview)),
                    "text/plain; charset=utf-8",
                    $"{baseName}.txt"),
                _ => BadRequest(new
                {
                    success = false,
                    error = "Formato inválido",
                    message = "Use format=txt, format=pdf ou format=docx"
                })
            };
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

    public async Task<IActionResult> StartVoiceInterview(
        VoiceInterviewStartSignature body,
        CancellationToken cancellationToken)
    {
        try
        {
            var accessError = await EnsureBundledServiceAccessAsync(body.AnalysisId, cancellationToken);
            if (accessError != null)
            {
                return accessError;
            }

            var (ctx, resolveError) = await ResolveAnalysisContextAsync(
                body.AnalysisId,
                body.Analysis,
                body.ResumeText,
                body.ResumeId,
                body.SiteId,
                cancellationToken);
            if (resolveError != null)
            {
                return resolveError;
            }

            if (string.IsNullOrWhiteSpace(ctx!.ResumeText))
            {
                return BadRequest(new
                {
                    success = false,
                    error = "Currículo não encontrado",
                    message = "O texto do currículo não está disponível no histórico. Faça uma nova análise."
                });
            }

            var userId = JwtAuthHelper.TryGetUserId(_http.HttpContext!.Request.Headers, _configuration);
            var result = await _voiceInterview.StartAsync(
                ctx.ResumeText!,
                ctx.Analysis,
                ctx.SiteId ?? body.SiteId,
                userId,
                ctx.ResumeId ?? body.ResumeId,
                cancellationToken);

            return Ok(new
            {
                success = true,
                simulationId = result.SimulationId,
                persona = result.Persona,
                openingMessage = result.OpeningMessage,
                mode = "voice_conversational"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao iniciar entrevista por voz");
            var statusCode = ex.Message.Contains("503", StringComparison.Ordinal) ? 503 : 500;
            return StatusCode(statusCode, new
            {
                success = false,
                error = "Erro ao iniciar entrevista por voz",
                message = MapAiErrorMessage(ex)
            });
        }
    }

    public async Task<IActionResult> VoiceInterviewTurn(
        VoiceInterviewTurnSignature body,
        CancellationToken cancellationToken)
    {
        try
        {
            var accessError = await EnsureBundledServiceAccessAsync(body.AnalysisId, cancellationToken);
            if (accessError != null)
            {
                return accessError;
            }

            var (ctx, resolveError) = await ResolveAnalysisContextAsync(
                body.AnalysisId,
                body.Analysis,
                body.ResumeText,
                resumeId: null,
                body.SiteId,
                cancellationToken);
            if (resolveError != null)
            {
                return resolveError;
            }

            if (string.IsNullOrWhiteSpace(ctx!.ResumeText) ||
                string.IsNullOrWhiteSpace(body.CandidateMessage))
            {
                return BadRequest(new
                {
                    success = false,
                    error = "Dados incompletos",
                    message = "É necessário resumeText e candidateMessage"
                });
            }

            var history = body.History ?? [];
            var turn = await _voiceInterview.ProcessTurnAsync(
                ctx.ResumeText!,
                ctx.Analysis,
                ctx.SiteId ?? body.SiteId,
                body.CandidateMessage.Trim(),
                history,
                body.TurnNumber > 0 ? body.TurnNumber : history.Count(m => m.Role == "candidate") + 1,
                cancellationToken);

            return Ok(new
            {
                success = true,
                interviewerMessage = turn.InterviewerMessage,
                shouldEnd = turn.ShouldEnd,
                phase = turn.Phase,
                turnNumber = turn.TurnNumber
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro no turno da entrevista por voz");
            return StatusCode(500, new
            {
                success = false,
                error = "Erro no turno da entrevista",
                message = ex.Message
            });
        }
    }

    public async Task<IActionResult> FinishVoiceInterview(
        VoiceInterviewFinishSignature body,
        CancellationToken cancellationToken)
    {
        try
        {
            var accessError = await EnsureBundledServiceAccessAsync(body.AnalysisId, cancellationToken);
            if (accessError != null)
            {
                return accessError;
            }

            var (finishCtx, finishResolveError) = await ResolveAnalysisContextAsync(
                body.AnalysisId,
                body.Analysis,
                body.ResumeText,
                resumeId: null,
                siteId: null,
                cancellationToken);
            if (finishResolveError != null)
            {
                return finishResolveError;
            }

            if (string.IsNullOrWhiteSpace(finishCtx!.ResumeText))
            {
                return BadRequest(new
                {
                    success = false,
                    error = "Currículo não encontrado",
                    message = "O texto do currículo não está disponível no histórico."
                });
            }

            var history = body.History ?? [];
            var summary = await _voiceInterview.FinishAsync(
                body.SimulationId,
                finishCtx.ResumeText!,
                finishCtx.Analysis,
                history,
                cancellationToken);

            await TryMarkServiceUsedAsync(body.AnalysisId, AnalysisBundledServiceKeys.Entrevista, cancellationToken);

            return Ok(new
            {
                success = true,
                score = summary.Score,
                summary,
                simulationId = body.SimulationId,
                message = "Entrevista por voz finalizada"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao finalizar entrevista por voz");
            return StatusCode(500, new
            {
                success = false,
                error = "Erro ao finalizar entrevista",
                message = ex.Message
            });
        }
    }

    public async Task<IActionResult> GetStructuredInterviewStatus(
        string? analysisId,
        CancellationToken cancellationToken)
    {
        try
        {
            var accessError = await EnsureBundledServiceAccessAsync(analysisId, cancellationToken);
            if (accessError != null)
            {
                return accessError;
            }

            if (string.IsNullOrWhiteSpace(analysisId))
            {
                return BadRequest(new { success = false, error = "analysisId é obrigatório" });
            }

            var userId = JwtAuthHelper.TryGetUserId(_http.HttpContext!.Request.Headers, _configuration);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { success = false, error = "Não autenticado" });
            }

            var analysisRow = await _analysis.GetAnalysisByIdAsync(analysisId, cancellationToken);
            var status = await _structuredInterview.GetStatusAsync(
                analysisId,
                userId,
                analysisRow?.IdCurriculo,
                cancellationToken);

            return Ok(new { success = true, status });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao consultar status da entrevista estruturada");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    public async Task<IActionResult> StartStructuredInterview(
        StructuredInterviewStartSignature body,
        CancellationToken cancellationToken)
    {
        try
        {
            var accessError = await EnsureBundledServiceAccessAsync(body.AnalysisId, cancellationToken);
            if (accessError != null)
            {
                return accessError;
            }

            var userId = JwtAuthHelper.TryGetUserId(_http.HttpContext!.Request.Headers, _configuration);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { success = false, error = "Não autenticado" });
            }

            if (!string.IsNullOrWhiteSpace(body.AnalysisId))
            {
                var existing = await _structuredInterview.GetStatusAsync(
                    body.AnalysisId,
                    userId,
                    body.ResumeId,
                    cancellationToken);
                if (existing.AlreadyCompleted)
                {
                    return BadRequest(new
                    {
                        success = false,
                        error = "Entrevista já realizada",
                        message = "Você já fez esta entrevista. Baixe o relatório disponível.",
                        simulationId = existing.SimulationId,
                        alreadyCompleted = true
                    });
                }
            }

            var (ctx, resolveError) = await ResolveAnalysisContextAsync(
                body.AnalysisId,
                body.Analysis,
                body.ResumeText,
                body.ResumeId,
                body.SiteId,
                cancellationToken);
            if (resolveError != null)
            {
                return resolveError;
            }

            if (string.IsNullOrWhiteSpace(ctx!.ResumeText))
            {
                return BadRequest(new
                {
                    success = false,
                    error = "Currículo não encontrado",
                    message = "O texto do currículo não está disponível."
                });
            }

            var result = await _structuredInterview.StartAsync(
                ctx.ResumeText!,
                ctx.Analysis,
                ctx.SiteId ?? body.SiteId,
                userId,
                ctx.ResumeId ?? body.ResumeId,
                cancellationToken);

            return Ok(new
            {
                success = true,
                simulationId = result.SimulationId,
                persona = result.Persona,
                candidateName = result.CandidateName,
                writtenQuestions = result.WrittenQuestions,
                phase1Minutes = result.Phase1Minutes,
                mode = "structured_written_then_voice"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao iniciar entrevista estruturada");
            return StatusCode(500, new
            {
                success = false,
                error = "Erro ao iniciar entrevista",
                message = MapAiErrorMessage(ex)
            });
        }
    }

    public async Task<IActionResult> BeginStructuredVoicePhase(
        StructuredInterviewBeginVoicePhaseSignature body,
        CancellationToken cancellationToken)
    {
        try
        {
            var accessError = await EnsureBundledServiceAccessAsync(body.AnalysisId, cancellationToken);
            if (accessError != null)
            {
                return accessError;
            }

            var (ctx, resolveError) = await ResolveAnalysisContextAsync(
                body.AnalysisId,
                body.Analysis,
                body.ResumeText,
                resumeId: null,
                body.SiteId,
                cancellationToken);
            if (resolveError != null)
            {
                return resolveError;
            }

            if (string.IsNullOrWhiteSpace(ctx!.ResumeText))
            {
                return BadRequest(new { success = false, error = "Currículo não encontrado" });
            }

            if (!string.IsNullOrWhiteSpace(body.SimulationId)
                && body.WrittenQuestions?.Count > 0
                && body.WrittenAnswers?.Count > 0)
            {
                await _structuredInterview.SaveWrittenAnswersAsync(
                    body.SimulationId,
                    body.WrittenQuestions,
                    body.WrittenAnswers,
                    cancellationToken);
            }

            var result = await _structuredInterview.BeginVoicePhaseAsync(
                ctx.ResumeText!,
                ctx.Analysis,
                ctx.SiteId ?? body.SiteId,
                body.CandidateName ?? "Candidato",
                cancellationToken);

            return Ok(new
            {
                success = true,
                introScript = result.IntroScript
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao iniciar fase de voz da entrevista");
            return StatusCode(500, new
            {
                success = false,
                error = "Erro ao preparar vídeo de apresentação",
                message = MapAiErrorMessage(ex)
            });
        }
    }

    public async Task<IActionResult> SubmitStructuredInterviewPhase(
        StructuredInterviewSubmitPhaseSignature body,
        CancellationToken cancellationToken)
    {
        try
        {
            var accessError = await EnsureBundledServiceAccessAsync(body.AnalysisId, cancellationToken);
            if (accessError != null)
            {
                return accessError;
            }

            if (string.IsNullOrWhiteSpace(body.SimulationId))
            {
                return BadRequest(new { success = false, error = "simulationId é obrigatório" });
            }

            await _structuredInterview.SavePhaseAsync(
                body.SimulationId,
                body.PhaseIndex,
                body.InterviewerScript ?? "",
                body.CandidateAnswer ?? "",
                cancellationToken);

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar fase da entrevista");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    public async Task<IActionResult> FinishStructuredInterview(
        StructuredInterviewFinishSignature body,
        CancellationToken cancellationToken)
    {
        try
        {
            var accessError = await EnsureBundledServiceAccessAsync(body.AnalysisId, cancellationToken);
            if (accessError != null)
            {
                return accessError;
            }

            var (ctx, resolveError) = await ResolveAnalysisContextAsync(
                body.AnalysisId,
                body.Analysis,
                body.ResumeText,
                resumeId: null,
                body.SiteId,
                cancellationToken);
            if (resolveError != null)
            {
                return resolveError;
            }

            if (string.IsNullOrWhiteSpace(ctx!.ResumeText))
            {
                return BadRequest(new { success = false, error = "Currículo não encontrado" });
            }

            var questions = body.WrittenQuestions ?? [];
            var answers = body.WrittenAnswers ?? [];
            var result = await _structuredInterview.FinishAsync(
                body.SimulationId,
                ctx.ResumeText!,
                ctx.Analysis,
                ctx.SiteId ?? body.SiteId,
                body.CandidateName ?? "Candidato",
                body.IntroScript ?? "",
                questions,
                answers,
                body.Phase1Answer ?? "",
                cancellationToken);

            await TryMarkServiceUsedAsync(body.AnalysisId, AnalysisBundledServiceKeys.Entrevista, cancellationToken);

            return Ok(new
            {
                success = true,
                score = result.Score,
                feedbackScript = result.FeedbackScript,
                summary = new
                {
                    score = result.Score,
                    overallFeedback = result.OverallFeedback,
                    strengths = result.Strengths,
                    improvements = result.Improvements
                },
                simulationId = result.SimulationId,
                message = "Entrevista finalizada"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao finalizar entrevista estruturada");
            return StatusCode(500, new
            {
                success = false,
                error = "Erro ao finalizar entrevista",
                message = MapAiErrorMessage(ex)
            });
        }
    }

    public Task<IActionResult> GetPricingConfig(CancellationToken cancellationToken = default) =>
        BuildPublicPricingResponseAsync(cancellationToken);

    public Task<IActionResult> GetPlans(CancellationToken cancellationToken = default) =>
        BuildPublicPricingResponseAsync(cancellationToken);

    private async Task<IActionResult> BuildPublicPricingResponseAsync(CancellationToken cancellationToken)
    {
        var config = await _pricing.GetPricingConfigAsync(cancellationToken);
        var pricingPlans = await _pricing.GetPricingPlansAsync(cancellationToken);
        var plans = pricingPlans.Values.Select(plan =>
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
        }).ToList();

        var analysisPlans = plans.Where(p => p.Id != "english").ToList();
        var englishPlan = plans.FirstOrDefault(p => p.Id == "english");

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
            },
            plans,
            analysisPlans,
            englishPlan,
            englishBundlePriceBRL = config.EnglishBundlePriceBRL,
            englishStandalonePriceBRL = config.EnglishPriceBRL,
            creditUnitPriceBRL = config.CreditUnitPriceBRL
        });
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
            var resolvedCpf = await ResolveAuthenticatedUserCpfAsync(couponCpf, cancellationToken);
            if (resolvedCpf == string.Empty)
            {
                return Ok(new
                {
                    success = true,
                    valid = false,
                    message = "Cadastre seu CPF no perfil antes de validar o cupom.",
                    code = "CPF_REQUIRED"
                });
            }

            var result = await _data.ValidateCouponAsync(couponCode.Trim(), resolvedCpf, cancellationToken);
            if (!result.Valid)
            {
                return Ok(new { success = true, valid = false, message = result.Message ?? "Cupom inválido ou inativo." });
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
            if (string.IsNullOrEmpty(body.PlanId))
            {
                return BadRequest(new { success = false, error = "Plano inválido" });
            }

            var userId = body.UserId ?? JwtAuthHelper.TryGetUserId(_http.HttpContext!.Request.Headers, _configuration);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { success = false, error = "É necessário estar autenticado para realizar a compra" });
            }

            if (body.PlanId == "english")
            {
                if (string.IsNullOrWhiteSpace(body.AnalysisId))
                {
                    return BadRequest(new
                    {
                        success = false,
                        error = "Análise obrigatória",
                        message = "O currículo em inglês deve ser comprado para uma análise existente."
                    });
                }

                if (!await _analysis.UserOwnsAnalysisAsync(userId, body.AnalysisId, cancellationToken))
                {
                    return StatusCode(403, new { success = false, error = "Análise não encontrada" });
                }

                if (await _analysis.HasEnglishPaidAsync(body.AnalysisId, cancellationToken))
                {
                    return BadRequest(new
                    {
                        success = false,
                        error = "Já adquirido",
                        message = "Esta análise já possui o currículo em inglês."
                    });
                }
            }
            else if (await _pricing.GetPlanAsync(body.PlanId, cancellationToken) == null)
            {
                return BadRequest(new { success = false, error = "Plano inválido" });
            }

            var user = await _data.GetUserProfileAsync(userId, cancellationToken);
            if (user == null)
            {
                return NotFound(new { success = false, error = "Usuário não encontrado" });
            }

            var cpf = ResolveProfileCpf(user);
            if (string.IsNullOrEmpty(cpf))
            {
                return BadRequest(new
                {
                    success = false,
                    error = "Cadastre seu CPF em Meus dados antes de comprar.",
                    code = "CPF_REQUIRED"
                });
            }

            var frontendUrl = _http.HttpContext!.Request.Headers.Origin.FirstOrDefault()
                ?? _configuration["FRONTEND_URL"];
            var couponCode = string.IsNullOrWhiteSpace(body.CouponCode) ? null : body.CouponCode.Trim();
            var includeEnglish = body.IncludeEnglish == true && body.PlanId != "english";

            var result = await _paymentProvider.CreateProviderCheckoutAsync(
                body.PlanId,
                userId,
                body.Email ?? string.Empty,
                frontendUrl,
                couponCode,
                cpf,
                includeEnglish,
                body.AnalysisId?.Trim(),
                cancellationToken);

            if (result.FreeCheckout)
            {
                var config = await _pricing.GetPricingConfigAsync(cancellationToken);
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
                    CpfNormalized = result.CpfNormalized,
                    IncludeEnglish = includeEnglish,
                    EnglishPriceBRL = config.EnglishBundlePriceBRL,
                    AnalysisId = body.AnalysisId?.Trim()
                }, cancellationToken);

                var baseUrl = (frontendUrl ?? _configuration["FRONTEND_URL"] ?? string.Empty).TrimEnd('/');
                return Ok(new
                {
                    success = true,
                    freeCheckout = true,
                    provider = result.Provider,
                    redirectUrl = BuildPaymentRedirectUrl(baseUrl, userId, body.PlanId, body.AnalysisId?.Trim()),
                    user = fulfillment.User
                });
            }

            return Ok(new
            {
                success = true,
                provider = result.Provider,
                transparentCheckout = result.TransparentCheckout,
                amountBRL = result.AmountBRL,
                publicKey = result.PublicKey,
                pixAvailable = result.PixAvailable,
                planId = result.PlanId,
                planName = result.PlanName,
                sessionId = result.SessionId,
                checkoutUrl = result.Url,
                preferenceId = result.PreferenceId,
                mercadoPagoLiveMode = result.LiveMode,
                payerEmail = result.PayerEmail
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, error = "Erro ao criar sessão de pagamento", message = ex.Message });
        }
    }

        public async Task<IActionResult> ProcessMercadoPagoCard(
        MercadoPagoCardPaymentSignature body,
        CancellationToken cancellationToken)
    {
        try
        {
            var purchase = await ResolvePurchaseRequestAsync(body, cancellationToken);
            if (purchase.ErrorResult != null)
            {
                return purchase.ErrorResult;
            }

            var includeEnglish = body.IncludeEnglish == true && body.PlanId != "english";
            var result = await _mercadoPago.ProcessCardPaymentAsync(
                body.PlanId!,
                purchase.UserId!,
                body.Email ?? string.Empty,
                body.Token ?? string.Empty,
                body.PaymentMethodId ?? string.Empty,
                body.IssuerId,
                body.Installments,
                string.IsNullOrWhiteSpace(body.CouponCode) ? null : body.CouponCode.Trim(),
                purchase.Cpf,
                includeEnglish,
                body.AnalysisId?.Trim(),
                cancellationToken);

            if (result.Paid)
            {
                return Ok(new
                {
                    success = true,
                    paid = true,
                    paymentId = result.PaymentId,
                    status = result.Status,
                    user = result.User,
                    alreadyFulfilled = result.AlreadyFulfilled
                });
            }

            return Ok(new
            {
                success = result.Success,
                paid = false,
                paymentId = result.PaymentId,
                status = result.Status,
                statusDetail = result.StatusDetail,
                message = result.Message ?? "Pagamento não aprovado."
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, error = "Erro ao processar cartão", message = ex.Message });
        }
    }

        public async Task<IActionResult> CreateMercadoPagoPix(
        CreatePaymentSessionSignature body,
        CancellationToken cancellationToken)
    {
        try
        {
            var purchase = await ResolvePurchaseRequestAsync(body, cancellationToken);
            if (purchase.ErrorResult != null)
            {
                return purchase.ErrorResult;
            }

            var includeEnglish = body.IncludeEnglish == true && body.PlanId != "english";
            var result = await _mercadoPago.CreatePixPaymentAsync(
                body.PlanId!,
                purchase.UserId!,
                body.Email ?? string.Empty,
                string.IsNullOrWhiteSpace(body.CouponCode) ? null : body.CouponCode.Trim(),
                purchase.Cpf,
                includeEnglish,
                body.AnalysisId?.Trim(),
                cancellationToken);

            if (!result.Success)
            {
                return BadRequest(new
                {
                    success = false,
                    error = result.Message ?? "Erro ao gerar PIX"
                });
            }

            return Ok(new
            {
                success = true,
                paymentId = result.PaymentId,
                status = result.Status,
                qrCode = result.QrCode,
                qrCodeBase64 = result.QrCodeBase64,
                ticketUrl = result.TicketUrl,
                expiration = result.Expiration,
                amountBRL = result.AmountBRL
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, error = "Erro ao gerar PIX", message = ex.Message });
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

        var planId = body.PlanId?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(planId))
        {
            return BadRequest(new { success = false, error = "planId é obrigatório" });
        }

        var config = await _pricing.GetPricingConfigAsync(cancellationToken);
        var paymentIdBase = $"admin_free_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{userId}";

        if (planId == "english")
        {
            if (string.IsNullOrWhiteSpace(body.AnalysisId))
            {
                return BadRequest(new
                {
                    success = false,
                    error = "analysisId é obrigatório para conceder inglês grátis"
                });
            }

            if (!await _analysis.UserOwnsAnalysisAsync(userId, body.AnalysisId, cancellationToken))
            {
                return StatusCode(403, new { success = false, error = "Análise não encontrada" });
            }

            if (await _analysis.HasEnglishPaidAsync(body.AnalysisId, cancellationToken))
            {
                return Ok(new
                {
                    success = true,
                    message = "Esta análise já possui currículo em inglês.",
                    analysisId = body.AnalysisId,
                    curriculo_ingles_pago = true
                });
            }

            await _data.CreatePurchaseAsync(
                userId,
                "english",
                "Currículo em Inglês (admin grátis)",
                0,
                0,
                paymentMethod: "admin_free",
                paymentId: $"{paymentIdBase}_english",
                serviceType: "curriculo_ingles",
                analysisId: body.AnalysisId.Trim(),
                cancellationToken: cancellationToken);

            await _analysis.GrantEnglishPaidAsync(body.AnalysisId.Trim(), cancellationToken);

            return Ok(new
            {
                success = true,
                message = "Currículo em inglês liberado gratuitamente para esta análise.",
                analysisId = body.AnalysisId,
                curriculo_ingles_pago = true
            });
        }

        var plan = await _pricing.GetPlanAsync(planId, cancellationToken);
        if (plan == null)
        {
            return BadRequest(new { success = false, error = "planId inválido. Use: single, pack3, pack5 ou english." });
        }

        var purchase = await _data.CreatePurchaseAsync(
            userId,
            plan.Id,
            plan.Name,
            plan.Analyses,
            0,
            paymentMethod: "admin_free",
            paymentId: paymentIdBase,
            serviceType: "analysis_plan",
            cancellationToken: cancellationToken);

        if (body.IncludeEnglish == true)
        {
            await _data.CreatePurchaseAsync(
                userId,
                "english",
                "Currículo em Inglês (admin grátis — bundle)",
                0,
                0,
                paymentMethod: "admin_free",
                paymentId: $"{paymentIdBase}_english_bundle",
                parentPurchaseId: purchase.Id,
                serviceType: "curriculo_ingles",
                cancellationToken: cancellationToken);
        }

        var credits = await _data.GetAvailableCreditsAsync(userId, cancellationToken);
        return Ok(new
        {
            success = true,
            message = body.IncludeEnglish == true
                ? $"{plan.Analyses} crédito(s) + add-on de inglês no próximo uso de crédito."
                : $"{plan.Analyses} crédito(s) adicionado(s) gratuitamente.",
            credits,
            includeEnglish = body.IncludeEnglish == true
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

        var englishCredits = await _data.GetPendingEnglishCreditsAsync(resolvedUserId, cancellationToken);

        return Ok(new
        {
            success = true,
            credits = user.Credits,
            englishCredits,
            plan = user.Plan,
            lastAnalysis = user.LastAnalysis
        });
    }

        public async Task<IActionResult> ListJobSites(CancellationToken cancellationToken)
    {
        try
        {
            var rows = await _data.GetActiveJobSitesAsync(cancellationToken);
            var sites = rows.Select(s => new JobSiteListItemDto
            {
                Id = s.Id,
                Nome = s.Nome ?? "",
                UrlBase = s.UrlBase,
                Descricao = s.Descricao,
                Ativo = s.Ativo ?? true
            }).ToList();

            return Ok(new { success = true, sites });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar sites de vagas");
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

            var analysisInput = PersistedAnalysisMapper.ToAnalysisInput(analysis);
            var resumeText = PersistedAnalysisMapper.GetResumeText(analysis);

            var servicos = await _analysis.GetServicesStatusAsync(analysisId, cancellationToken);

            return Ok(new
            {
                success = true,
                analysis,
                analysisForServices = analysisInput,
                originalText = resumeText ?? string.Empty,
                resumeId = analysis.IdCurriculo,
                siteId = analysis.IdSiteVagas,
                servicos
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

    public async Task<IActionResult> GetPendingServices(CancellationToken cancellationToken = default)
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

            var summary = await _analysis.GetPendingServicesSummaryAsync(userId, cancellationToken);
            return Ok(new
            {
                success = true,
                totalServicosPendentes = summary.TotalServicosPendentes,
                analisesComPendencias = summary.AnalisesComPendencias,
                analises = summary.Analises
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar serviços pendentes");
            return StatusCode(500, new
            {
                success = false,
                error = "Erro ao listar serviços pendentes",
                message = ex.Message
            });
        }
    }

    private async Task TryMarkServiceUsedAsync(
        string? analysisId,
        string serviceKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(analysisId))
        {
            return;
        }

        try
        {
            await _analysis.MarkServiceUsedAsync(analysisId, serviceKey, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Não foi possível marcar serviço {ServiceKey} na análise {AnalysisId}",
                serviceKey,
                analysisId);
        }
    }

    private async Task TryMarkEnglishFormatUsedAsync(
        string? analysisId,
        string formatKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(analysisId))
        {
            return;
        }

        try
        {
            await _analysis.MarkServiceUsedAsync(analysisId, formatKey, cancellationToken);

            var status = await _analysis.GetServicesStatusAsync(analysisId, cancellationToken);
            if (status.CurriculoInglesPdf && status.CurriculoInglesWord)
            {
                await _analysis.MarkServiceUsedAsync(
                    analysisId,
                    AnalysisBundledServiceKeys.CurriculoIngles,
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Não foi possível marcar formato inglês {FormatKey} na análise {AnalysisId}",
                formatKey,
                analysisId);
        }
    }

    private async Task<string?> ResolveAnalysisIdForInterviewAsync(
        string? analysisId,
        string? simulationId,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(analysisId))
        {
            return analysisId;
        }

        if (string.IsNullOrWhiteSpace(simulationId))
        {
            return null;
        }

        var userId = JwtAuthHelper.TryGetUserId(_http.HttpContext!.Request.Headers, _configuration);
        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        var interview = await _data.GetInterviewByIdAsync(simulationId, cancellationToken);
        if (interview?.IdCurriculo == null)
        {
            return null;
        }

        return await _analysis.GetAnalysisIdByResumeIdAsync(userId, interview.IdCurriculo, cancellationToken);
    }

    /// <summary>
    /// Serviços inclusos na análise (entrevista, carta, PDF melhorado) não consomem novo crédito.
    /// Com analysisId, exige que a análise pertença ao usuário autenticado.
    /// </summary>
    private async Task<IActionResult?> EnsureBundledServiceAccessAsync(
        string? analysisId,
        CancellationToken cancellationToken)
    {
        var userId = JwtAuthHelper.TryGetUserId(_http.HttpContext!.Request.Headers, _configuration);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new
            {
                success = false,
                error = "Não autenticado",
                message = "É necessário estar logado para usar os serviços da sua análise."
            });
        }

        if (string.IsNullOrWhiteSpace(analysisId))
        {
            return null;
        }

        if (!await _analysis.UserOwnsAnalysisAsync(userId, analysisId, cancellationToken))
        {
            return StatusCode(403, new
            {
                success = false,
                error = "Acesso negado",
                message = "Análise não encontrada ou não pertence à sua conta."
            });
        }

        return null;
    }

    private async Task<IActionResult?> EnsureEnglishResumeAccessAsync(
        string? analysisId,
        CancellationToken cancellationToken)
    {
        var authError = await EnsureBundledServiceAccessAsync(analysisId, cancellationToken);
        if (authError != null)
        {
            return authError;
        }

        if (string.IsNullOrWhiteSpace(analysisId))
        {
            return BadRequest(new
            {
                success = false,
                error = "Análise obrigatória",
                message = "Informe a análise para gerar o currículo em inglês."
            });
        }

        if (!await _analysis.HasEnglishPaidAsync(analysisId, cancellationToken))
        {
            var userId = JwtAuthHelper.TryGetUserId(_http.HttpContext!.Request.Headers, _configuration);
            if (!string.IsNullOrEmpty(userId))
            {
                var profile = await _data.GetUserProfileAsync(userId, cancellationToken);
                if (profile?.UserType == "admin")
                {
                    await _analysis.GrantEnglishPaidAsync(analysisId.Trim(), cancellationToken);
                    _logger.LogInformation(
                        "Admin {UserId}: inglês liberado automaticamente para análise {AnalysisId}",
                        userId,
                        analysisId);
                    return null;
                }
            }

            return StatusCode(402, new
            {
                success = false,
                error = "Ingles_nao_adquirido",
                code = "ENGLISH_NOT_PURCHASED",
                message = "Compre o currículo em inglês para esta análise antes de gerar o arquivo."
            });
        }

        return null;
    }

    /// <summary>
    /// Carrega análise e texto do currículo do banco quando analysisId é informado (histórico).
    /// </summary>
    private async Task<(ResolvedAnalysisContext? Context, IActionResult? Error)> ResolveAnalysisContextAsync(
        string? analysisId,
        AnalysisInput? clientAnalysis,
        string? clientResumeText,
        string? resumeId,
        string? siteId,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(analysisId))
        {
            var persisted = await _analysis.GetAnalysisByIdAsync(analysisId, cancellationToken);
            if (persisted == null)
            {
                return (null, NotFound(new
                {
                    success = false,
                    error = "Análise não encontrada",
                    message = "Análise não encontrada no histórico"
                }));
            }

            var resumeText = PersistedAnalysisMapper.GetResumeText(persisted);
            if (string.IsNullOrWhiteSpace(resumeText) && !string.IsNullOrWhiteSpace(clientResumeText))
            {
                resumeText = clientResumeText.Trim();
            }

            return (new ResolvedAnalysisContext
            {
                Analysis = PersistedAnalysisMapper.ToAnalysisInput(persisted),
                ResumeText = resumeText,
                ResumeId = PersistedAnalysisMapper.GetResumeId(persisted) ?? resumeId,
                SiteId = PersistedAnalysisMapper.GetSiteId(persisted) ?? siteId
            }, null);
        }

        if (clientAnalysis == null)
        {
            return (null, BadRequest(new
            {
                success = false,
                error = "Dados incompletos",
                message = "É necessário fornecer analysis ou analysisId"
            }));
        }

        return (new ResolvedAnalysisContext
        {
            Analysis = clientAnalysis,
            ResumeText = clientResumeText?.Trim(),
            ResumeId = resumeId,
            SiteId = siteId
        }, null);
    }

    private static string MapAiErrorMessage(Exception ex)
    {
        var msg = ex.Message ?? "";
        if (msg.Contains("503", StringComparison.Ordinal) || msg.Contains("high demand", StringComparison.OrdinalIgnoreCase))
        {
            return "O serviço de IA está com alta demanda no momento. Aguarde alguns minutos e tente novamente — isso não consome um novo crédito.";
        }

        if (msg.Contains("429", StringComparison.Ordinal))
        {
            return "Limite temporário da IA atingido. Tente novamente em alguns minutos.";
        }

        return msg;
    }

    private sealed class PurchaseRequestResolution
    {
        public string? UserId { get; init; }
        public string? Cpf { get; init; }
        public IActionResult? ErrorResult { get; init; }
    }

    private async Task<PurchaseRequestResolution> ResolvePurchaseRequestAsync(
        CreatePaymentSessionSignature body,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(body.PlanId))
        {
            return new PurchaseRequestResolution
            {
                ErrorResult = BadRequest(new { success = false, error = "Plano inválido" })
            };
        }

        var userId = body.UserId ?? JwtAuthHelper.TryGetUserId(_http.HttpContext!.Request.Headers, _configuration);
        if (string.IsNullOrEmpty(userId))
        {
            return new PurchaseRequestResolution
            {
                ErrorResult = Unauthorized(new { success = false, error = "É necessário estar autenticado para realizar a compra" })
            };
        }

        if (body.PlanId == "english")
        {
            if (string.IsNullOrWhiteSpace(body.AnalysisId))
            {
                return new PurchaseRequestResolution
                {
                    ErrorResult = BadRequest(new
                    {
                        success = false,
                        error = "Análise obrigatória",
                        message = "O currículo em inglês deve ser comprado para uma análise existente."
                    })
                };
            }

            if (!await _analysis.UserOwnsAnalysisAsync(userId, body.AnalysisId, cancellationToken))
            {
                return new PurchaseRequestResolution
                {
                    ErrorResult = StatusCode(403, new { success = false, error = "Análise não encontrada" })
                };
            }

            if (await _analysis.HasEnglishPaidAsync(body.AnalysisId, cancellationToken))
            {
                return new PurchaseRequestResolution
                {
                    ErrorResult = BadRequest(new
                    {
                        success = false,
                        error = "Já adquirido",
                        message = "Esta análise já possui o currículo em inglês."
                    })
                };
            }
        }
        else if (await _pricing.GetPlanAsync(body.PlanId, cancellationToken) == null)
        {
            return new PurchaseRequestResolution
            {
                ErrorResult = BadRequest(new { success = false, error = "Plano inválido" })
            };
        }

        var user = await _data.GetUserProfileAsync(userId, cancellationToken);
        if (user == null)
        {
            return new PurchaseRequestResolution
            {
                ErrorResult = NotFound(new { success = false, error = "Usuário não encontrado" })
            };
        }

        var cpf = ResolveProfileCpf(user);
        if (string.IsNullOrEmpty(cpf))
        {
            return new PurchaseRequestResolution
            {
                ErrorResult = BadRequest(new
                {
                    success = false,
                    error = "Cadastre seu CPF em Meus dados antes de comprar.",
                    code = "CPF_REQUIRED"
                })
            };
        }

        return new PurchaseRequestResolution { UserId = userId, Cpf = cpf };
    }

    private static string BuildPaymentRedirectUrl(
        string baseUrl,
        string userId,
        string planId,
        string? analysisId)
    {
        return CurriculosProIA.Service.Helpers.PaymentReturnUrls.Build(
            baseUrl,
            CurriculosProIA.Service.Helpers.PaymentReturnUrls.SuccessPath,
            "free",
            userId,
            analysisId,
            englishPaid: planId == "english",
            freeCheckout: true);
    }

    private async Task<string?> ResolveAuthenticatedUserCpfAsync(string? fallbackCpf, CancellationToken cancellationToken)
    {
        var userId = JwtAuthHelper.TryGetUserId(_http.HttpContext!.Request.Headers, _configuration);
        if (string.IsNullOrEmpty(userId))
        {
            var normalized = string.IsNullOrWhiteSpace(fallbackCpf)
                ? null
                : Regex.Replace(fallbackCpf, @"\D", string.Empty);
            return normalized?.Length == 11 ? normalized : null;
        }

        var user = await _data.GetUserProfileAsync(userId, cancellationToken);
        var profileCpf = user == null ? null : ResolveProfileCpf(user);
        if (!string.IsNullOrEmpty(profileCpf))
        {
            return profileCpf;
        }

        return string.Empty;
    }

    private static string? ResolveProfileCpf(UserProfile user)
    {
        var cpf = user.Cpf?.Trim();
        if (string.IsNullOrWhiteSpace(cpf))
        {
            return null;
        }

        var normalized = Regex.Replace(cpf, @"\D", string.Empty);
        return normalized.Length == 11 ? normalized : null;
    }

}
