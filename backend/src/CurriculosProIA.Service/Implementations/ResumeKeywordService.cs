using System.Text.Json;
using System.Text.RegularExpressions;
using CurriculosProIA.Domain.Dtos;
using CurriculosProIA.Repository.Persistence;
using CurriculosProIA.Service.Interfaces;
using Microsoft.Extensions.Logging;

namespace CurriculosProIA.Service.Implementations;

public class ResumeKeywordService : IResumeKeywordService
{
    private readonly IAiService _aiService;
    private readonly ILogger<ResumeKeywordService> _logger;

    private static readonly string[] TechPatterns =
    [
        "javascript", "typescript", "python", "java", "c#", "c++", "php", "ruby", "go", "rust",
        "react", "angular", "vue", "node.js", "express", "django", "flask", "spring", "laravel",
        "sql", "mysql", "postgresql", "mongodb", "redis", "aws", "azure", "docker", "kubernetes",
        "git", ".net", "asp.net", "entity framework", "scrum", "agile", "ci/cd", "devops"
    ];

    public ResumeKeywordService(IAiService aiService, ILogger<ResumeKeywordService> logger)
    {
        _aiService = aiService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<string>> GenerateKeywordsAsync(
        string resumeText,
        AnalysisInput analysis,
        SiteVagasRow? site,
        CancellationToken cancellationToken = default)
    {
        var fromAnalysis = ExtractFromAnalysis(analysis, resumeText);
        var siteDefaults = site?.PalavrasChavePadrao ?? new List<string>();
        var aiKeywords = await TryGenerateWithAiAsync(resumeText, analysis, site, cancellationToken);

        return siteDefaults
            .Concat(fromAnalysis)
            .Concat(aiKeywords)
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(25)
            .ToList();
    }

    private async Task<List<string>> TryGenerateWithAiAsync(
        string resumeText,
        AnalysisInput analysis,
        SiteVagasRow? site,
        CancellationToken cancellationToken)
    {
        try
        {
            var siteName = site?.Nome ?? "portal de vagas";
            var characteristics = site?.Caracteristicas != null
                ? JsonSerializer.Serialize(site.Caracteristicas, new JsonSerializerOptions { WriteIndented = true })
                : "{}";
            var siteKeywords = site?.PalavrasChavePadrao != null
                ? string.Join(", ", site.PalavrasChavePadrao)
                : "Nenhuma";

            var snippet = resumeText.Length > 2500 ? resumeText[..2500] : resumeText;
            var prompt = $"""
                Você é especialista em ATS e otimização de currículos para {siteName}.
                Extraia palavras-chave técnicas e de mercado para um currículo otimizado.

                CURRÍCULO:
                {snippet}

                ANÁLISE:
                - Habilidades: {(analysis.Habilidades != null ? string.Join(", ", analysis.Habilidades) : "N/A")}
                - Experiência: {analysis.Experiencia ?? "N/A"}
                - Área: {analysis.AreaAtuacao ?? "N/A"}
                - Pontos fortes: {(analysis.PontosFortes != null ? string.Join(", ", analysis.PontosFortes.Take(5)) : "N/A")}

                SITE: {siteName}
                Características: {characteristics}
                Keywords padrão do site: {siteKeywords}

                Regras:
                1. Retorne 12-18 termos: tecnologias, cargos, metodologias e competências REAIS do candidato
                2. Priorize termos que passam em ATS de {siteName}
                3. Não invente tecnologias ausentes no currículo
                4. Responda APENAS com JSON array de strings

                ["termo1", "termo2"]
                """;

            var response = await _aiService.GenerateTextAsync(prompt, 0.4, 800, cancellationToken);
            var cleaned = AiService.CleanMarkdownFence(response);
            var match = Regex.Match(cleaned, @"\[.*\]", RegexOptions.Singleline);
            if (!match.Success)
            {
                return new List<string>();
            }

            var keywords = JsonSerializer.Deserialize<List<string>>(match.Value);
            return keywords?.Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => k.Trim()).ToList()
                   ?? new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao gerar palavras-chave dinâmicas para currículo");
            return new List<string>();
        }
    }

    private static List<string> ExtractFromAnalysis(AnalysisInput analysis, string resumeText)
    {
        var keywords = new List<string>();

        if (analysis.Habilidades != null)
        {
            keywords.AddRange(analysis.Habilidades);
        }

        if (!string.IsNullOrEmpty(analysis.AreaAtuacao))
        {
            keywords.Add(analysis.AreaAtuacao);
        }

        if (!string.IsNullOrEmpty(analysis.Experiencia))
        {
            keywords.AddRange(ExtractTechKeywords(analysis.Experiencia));
        }

        keywords.AddRange(ExtractTechKeywords(resumeText));

        if (analysis.PontosFortes != null)
        {
            foreach (var ponto in analysis.PontosFortes.Where(p => p.Length < 60))
            {
                keywords.AddRange(ExtractTechKeywords(ponto));
            }
        }

        return keywords
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(15)
            .ToList();
    }

    private static List<string> ExtractTechKeywords(string text)
    {
        var keywords = new List<string>();
        foreach (var tech in TechPatterns)
        {
            if (text.Contains(tech, StringComparison.OrdinalIgnoreCase))
            {
                keywords.Add(tech);
            }
        }

        return keywords;
    }
}
