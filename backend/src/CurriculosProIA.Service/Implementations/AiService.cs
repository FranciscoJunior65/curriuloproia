using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CurriculosProIA.Domain.Entities;
using CurriculosProIA.Domain.Dtos;
using CurriculosProIA.Repository.Persistence;
using CurriculosProIA.Domain.Dtos;

using CurriculosProIA.Service.Interfaces;
using CurriculosProIA.Service.Helpers;
using CurriculosProIA.Repository.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CurriculosProIA.Service.Implementations;

public class AiService : IAiService
{
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IJobSiteRepository _jobSites;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AiService> _logger;

    public AiService(
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        IJobSiteRepository jobSites,
        IHttpClientFactory httpClientFactory,
        ILogger<AiService> logger)
    {
        _configuration = configuration;
        _hostEnvironment = hostEnvironment;
        _jobSites = jobSites;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<ResumeAnalysisResult> AnalyzeResumeAsync(
        string resumeText,
        string? userId = null,
        string? curriculoId = null,
        string? siteId = null,
        CancellationToken cancellationToken = default)
    {
        if (UseMockAi())
        {
            return await AnalyzeResumeWithMockAsync(resumeText, siteId, cancellationToken);
        }

        try
        {
            return await AnalyzeResumeWithGeminiAsync(resumeText, siteId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro no provedor de IA na análise de currículo");
            throw;
        }
    }

    private bool UseMockAi() => AiRuntimeOptions.UseMockAi(_configuration, _hostEnvironment);

    private static readonly string[] DefaultGeminiFallbackModels =
    [
        "gemini-2.5-flash",
        "gemini-2.5-flash-lite",
        "gemini-2.0-flash"
    ];

    private static readonly IReadOnlyDictionary<string, string> DeprecatedGeminiModelMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["gemini-pro"] = "gemini-2.5-flash",
            ["gemini-1.5-flash"] = "gemini-2.5-flash",
            ["gemini-1.5-flash-latest"] = "gemini-2.5-flash",
            ["gemini-1.5-flash-8b"] = "gemini-2.5-flash-lite",
            ["gemini-1.5-pro"] = "gemini-2.5-flash",
            ["gemini-1.5-pro-latest"] = "gemini-2.5-flash"
        };

    private string GeminiModel
    {
        get
        {
            var model = _configuration["GEMINI_MODEL"] ?? "gemini-2.5-flash";
            return NormalizeGeminiModel(model);
        }
    }

    private static string NormalizeGeminiModel(string model)
    {
        var trimmed = model.Trim();
        return DeprecatedGeminiModelMap.TryGetValue(trimmed, out var mapped) ? mapped : trimmed;
    }

    private IReadOnlyList<string> GetGeminiModelChain()
    {
        var configuredFallbacks = _configuration["GEMINI_FALLBACK_MODELS"];
        var fallbacks = string.IsNullOrWhiteSpace(configuredFallbacks)
            ? DefaultGeminiFallbackModels
            : configuredFallbacks.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return new[] { GeminiModel }
            .Concat(fallbacks.Select(NormalizeGeminiModel))
            .Where(model => !string.IsNullOrWhiteSpace(model))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsTransientGeminiStatus(System.Net.HttpStatusCode statusCode) =>
        statusCode == System.Net.HttpStatusCode.ServiceUnavailable
        || statusCode == System.Net.HttpStatusCode.TooManyRequests
        || statusCode == System.Net.HttpStatusCode.GatewayTimeout
        || statusCode == System.Net.HttpStatusCode.RequestTimeout;

    private static bool IsTransientGeminiError(Exception ex)
    {
        var msg = ex.Message ?? string.Empty;
        return msg.Contains("503", StringComparison.Ordinal)
            || msg.Contains("429", StringComparison.Ordinal)
            || msg.Contains("502", StringComparison.Ordinal)
            || msg.Contains("504", StringComparison.Ordinal)
            || msg.Contains("404", StringComparison.Ordinal)
            || msg.Contains("high demand", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("UNAVAILABLE", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("NOT_FOUND", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("is not found for API version", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("is not supported for generateContent", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<ResumeAnalysisResult> AnalyzeResumeWithGeminiAsync(
        string resumeText,
        string? siteId,
        CancellationToken cancellationToken)
    {
        var apiKey = _configuration["GEMINI_API_KEY"];
        GeminiApiKeyValidator.EnsureValidOrThrow(apiKey);

        var validatedText = ValidateAndTruncateText(resumeText);
        var siteInfo = await BuildSiteInfoAsync(siteId, cancellationToken);

        var systemPrompt = $"""
            Você é um especialista em Recursos Humanos e análise de currículos com mais de 10 anos de experiência.
            {(siteId != null ? "IMPORTANTE: Esta análise é ESPECÍFICA para o site selecionado." : "")}
            Seja sempre construtivo e específico em suas análises.
            """;

        var userPrompt = $$"""
            Analise o seguinte currículo e forneça uma análise completa e detalhada em formato JSON.
            {{siteInfo}}
            FORMATO DE RESPOSTA (JSON obrigatório):
            {
              "pontosFortes": ["ponto 1", "ponto 2"],
              "pontosMelhorar": ["ponto 1", "ponto 2"],
              "experiencia": "resumo detalhado da experiência profissional",
              "formacao": "resumo da formação acadêmica",
              "habilidades": ["habilidade 1", "habilidade 2"],
              "recomendacoes": ["recomendação 1", "recomendação 2"],
              "score": 85
            }

            CURRÍCULO PARA ANÁLISE:
            {{validatedText}}

            IMPORTANTE: Responda APENAS com o JSON válido, sem texto adicional antes ou depois.
            """;

        var responseContent = await GenerateGeminiTextAsync(
            $"{systemPrompt}\n\n{userPrompt}",
            temperature: 0.7,
            maxOutputTokens: 4000,
            cancellationToken);

        responseContent = CleanJsonResponse(responseContent);
        var analysis = ParseAnalysisJson(responseContent);
        ValidateAnalysisStructure(analysis);
        return analysis;
    }

    private async Task<string> BuildSiteInfoAsync(string? siteId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(siteId))
        {
            return string.Empty;
        }

        try
        {
            var site = await _jobSites.GetJobSiteByIdAsync(siteId, cancellationToken);
            if (site == null)
            {
                return string.Empty;
            }

            var keywords = site.PalavrasChavePadrao ?? new List<string>();
            var characteristics = site.Caracteristicas != null
                ? JsonSerializer.Serialize(site.Caracteristicas)
                : "{}";

            return $"""

                SITE DE VAGAS SELECIONADO: {site.Nome}
                {(string.IsNullOrEmpty(site.Descricao) ? "" : $"Descrição: {site.Descricao}")}
                PALAVRAS-CHAVE: {(keywords.Count > 0 ? string.Join(", ", keywords) : "Nenhuma")}
                CARACTERÍSTICAS: {characteristics}
                """;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao buscar informações do site");
            return string.Empty;
        }
    }

    private async Task<ResumeAnalysisResult> AnalyzeResumeWithMockAsync(
        string resumeText,
        string? siteId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Usando análise MOCKADA (não consome créditos de IA)");
        await Task.Delay(1000, cancellationToken);

        var siteName = string.Empty;
        var siteKeywords = new List<string>();
        if (!string.IsNullOrEmpty(siteId))
        {
            var site = await _jobSites.GetJobSiteByIdAsync(siteId, cancellationToken);
            if (site != null)
            {
                siteName = site.Nome ?? "site selecionado";
                siteKeywords = site.PalavrasChavePadrao ?? new List<string>();
            }
        }

        var textLower = resumeText.ToLowerInvariant();
        var hasEmail = textLower.Contains('@') || Regex.IsMatch(resumeText, @"\b[\w\.-]+@[\w\.-]+\.\w+\b");
        var hasPhone = Regex.IsMatch(resumeText, @"\d{10,}") || Regex.IsMatch(resumeText, @"\(\d{2}\)\s?\d{4,5}-?\d{4}");
        var hasExperience = Regex.IsMatch(resumeText, @"experiência|experience|trabalho|work|empresa|company|profissional|professional", RegexOptions.IgnoreCase);
        var hasEducation = Regex.IsMatch(resumeText, @"formação|education|graduação|graduation|curso|course|universidade|university|faculdade|college", RegexOptions.IgnoreCase);
        var hasSkills = Regex.IsMatch(resumeText, @"habilidade|skill|competência|competency|conhecimento|knowledge", RegexOptions.IgnoreCase);
        var hasSiteKeywords = siteKeywords.Count > 0 && siteKeywords.Any(k => textLower.Contains(k.ToLowerInvariant()));

        var score = 50;
        if (hasEmail) score += 10;
        if (hasPhone) score += 10;
        if (hasExperience) score += 15;
        if (hasEducation) score += 15;
        if (hasSkills) score += 10;
        if (resumeText.Length > 500) score += 5;
        if (resumeText.Length > 1000) score += 5;
        if (hasSiteKeywords) score += 5;
        score = Math.Clamp(score, 0, 100);

        var pontosFortes = new List<string>();
        if (hasEmail) pontosFortes.Add("Email de contato presente");
        if (hasPhone) pontosFortes.Add("Telefone de contato presente");
        if (hasExperience) pontosFortes.Add("Experiência profissional mencionada");
        if (hasEducation) pontosFortes.Add("Formação acadêmica mencionada");
        if (hasSkills) pontosFortes.Add("Habilidades e competências destacadas");
        if (resumeText.Length > 500) pontosFortes.Add("Currículo com conteúdo detalhado");
        if (!string.IsNullOrEmpty(siteName)) pontosFortes.Add($"Análise otimizada para {siteName}");
        if (hasSiteKeywords) pontosFortes.Add($"Palavras-chave relevantes para {siteName} presentes");
        if (pontosFortes.Count == 0) pontosFortes.Add("Estrutura básica do currículo presente");

        var pontosMelhorar = new List<string>();
        if (!hasEmail) pontosMelhorar.Add("Adicione um email de contato profissional");
        if (!hasPhone) pontosMelhorar.Add("Adicione um telefone de contato");
        if (!hasExperience) pontosMelhorar.Add("Destaque sua experiência profissional com períodos e responsabilidades");
        if (!hasEducation) pontosMelhorar.Add("Mencione sua formação acadêmica com instituições e períodos");
        if (!hasSkills) pontosMelhorar.Add("Liste suas principais habilidades técnicas e comportamentais");
        if (resumeText.Length < 500) pontosMelhorar.Add("Adicione mais detalhes e informações relevantes");
        if (pontosMelhorar.Count == 0) pontosMelhorar.Add("Revise a formatação e organização do currículo");

        var habilidades = new List<string>();
        if (Regex.IsMatch(resumeText, @"javascript|js|node|react|angular|vue", RegexOptions.IgnoreCase)) habilidades.Add("JavaScript");
        if (Regex.IsMatch(resumeText, @"python|django|flask", RegexOptions.IgnoreCase)) habilidades.Add("Python");
        if (Regex.IsMatch(resumeText, @"java|spring", RegexOptions.IgnoreCase)) habilidades.Add("Java");
        if (Regex.IsMatch(resumeText, @"sql|database|banco de dados", RegexOptions.IgnoreCase)) habilidades.Add("Banco de Dados");
        if (habilidades.Count == 0)
        {
            habilidades.AddRange(["Comunicação", "Trabalho em Equipe", "Organização", "Proatividade"]);
        }

        List<string> recomendacoes = !string.IsNullOrEmpty(siteName)
            ?
            [
                $"Otimize o currículo especificamente para {siteName}, destacando palavras-chave relevantes",
                "Revise e atualize suas informações de contato (email e telefone)",
                "Destaque suas principais conquistas e resultados quantificáveis"
            ]
            :
            [
                "Revise e atualize suas informações de contato (email e telefone)",
                "Destaque suas principais conquistas e resultados quantificáveis",
                "Organize as informações de forma clara e cronológica"
            ];

        return new ResumeAnalysisResult
        {
            PontosFortes = pontosFortes.Take(5).ToList(),
            PontosMelhorar = pontosMelhorar.Take(5).ToList(),
            Experiencia = hasExperience
                ? $"Experiência profissional identificada no currículo.{(string.IsNullOrEmpty(siteName) ? "" : $" Considere adaptar para {siteName}.")}"
                : "Experiência profissional não encontrada ou não detalhada.",
            Formacao = hasEducation
                ? $"Formação acadêmica identificada.{(string.IsNullOrEmpty(siteName) ? "" : $" Destaque formações relevantes para {siteName}.")}"
                : "Formação acadêmica não encontrada ou não detalhada.",
            Habilidades = habilidades.Take(10).ToList(),
            Recomendacoes = recomendacoes,
            Score = score
        };
    }

    private static string ValidateAndTruncateText(string text, int maxLength = 15000)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("Texto do currículo está vazio");
        }

        if (text.Length > maxLength)
        {
            return text[..maxLength] + "... [texto truncado]";
        }

        return text;
    }

    private static string CleanJsonResponse(string content)
    {
        content = content.Trim();
        if (content.StartsWith("```json", StringComparison.Ordinal))
        {
            content = content["```json".Length..].Trim();
        }
        else if (content.StartsWith("```", StringComparison.Ordinal))
        {
            content = content["```".Length..].Trim();
        }

        if (content.EndsWith("```", StringComparison.Ordinal))
        {
            content = content[..^3].Trim();
        }

        return content;
    }

    private static ResumeAnalysisResult ParseAnalysisJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<ResumeAnalysisResult>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("JSON de análise inválido");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Resposta da IA não está em formato JSON válido: {ex.Message}", ex);
        }
    }

    private static void ValidateAnalysisStructure(ResumeAnalysisResult analysis)
    {
        if (analysis.PontosFortes == null || analysis.PontosMelhorar == null || analysis.Habilidades == null || analysis.Recomendacoes == null)
        {
            throw new InvalidOperationException("Campos obrigatórios ausentes na análise");
        }

        if (analysis.Score is < 0 or > 100)
        {
            throw new InvalidOperationException("score deve ser um número entre 0 e 100");
        }
    }

    public Task<string> GenerateTextAsync(
        string prompt,
        double temperature = 0.7,
        int maxOutputTokens = 3000,
        CancellationToken cancellationToken = default)
    {
        if (UseMockAi())
        {
            throw new InvalidOperationException(
                "Geração em modo mock desativada para este ambiente. Defina USE_MOCK_AI=false e configure GEMINI_API_KEY.");
        }

        return GenerateGeminiTextAsync(prompt, temperature, maxOutputTokens, cancellationToken);
    }

    private async Task<string> GenerateGeminiTextAsync(
        string prompt,
        double temperature,
        int maxOutputTokens,
        CancellationToken cancellationToken)
    {
        var apiKey = _configuration["GEMINI_API_KEY"];
        GeminiApiKeyValidator.EnsureValidOrThrow(apiKey);

        Exception? lastError = null;
        foreach (var model in GetGeminiModelChain())
        {
            try
            {
                var text = await GenerateGeminiTextWithModelAsync(
                    apiKey!,
                    model,
                    prompt,
                    temperature,
                    maxOutputTokens,
                    cancellationToken);
                return CleanMarkdownFence(text);
            }
            catch (InvalidOperationException ex) when (IsTransientGeminiError(ex))
            {
                lastError = ex;
                _logger.LogWarning(
                    "Gemini modelo {Model} indisponível ({Message}). Tentando fallback...",
                    model,
                    ex.Message);
            }
        }

        throw lastError ?? new InvalidOperationException("Gemini API error: falha após tentativas em todos os modelos.");
    }

    private async Task<string> GenerateGeminiTextWithModelAsync(
        string apiKey,
        string model,
        string prompt,
        double temperature,
        int maxOutputTokens,
        CancellationToken cancellationToken)
    {
        var requestBody = GeminiRequestBuilder.BuildGenerateContentRequest(
            prompt,
            temperature,
            maxOutputTokens,
            model);

        var client = _httpClientFactory.CreateClient("Gemini");
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";

        const int maxAttempts = 5;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);
            request.Content = JsonContent.Create(requestBody);

            using var response = await client.SendAsync(request, cancellationToken);
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return GeminiResponseParser.ExtractText(payload);
            }

            if (IsTransientGeminiStatus(response.StatusCode) && attempt < maxAttempts)
            {
                var delaySeconds = Math.Min(30, (int)Math.Pow(2, attempt));
                _logger.LogWarning(
                    "Gemini {Status} ({Model}) tentativa {Attempt}/{Max}. Aguardando {Delay}s...",
                    response.StatusCode,
                    model,
                    attempt,
                    maxAttempts,
                    delaySeconds);
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                continue;
            }

            throw new InvalidOperationException($"Gemini API error: {response.StatusCode} - {payload}");
        }

        throw new InvalidOperationException($"Gemini API error: falha após {maxAttempts} tentativas ({model}).");
    }

    internal static string CleanMarkdownFence(string content)
    {
        if (!content.StartsWith("```", StringComparison.Ordinal))
        {
            return content;
        }

        var cleaned = Regex.Replace(content, @"^```[a-z]*\s*", "", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\s*```$", "");
        return cleaned.Trim();
    }
}
