using System.Text;
using System.Text.Json;
using CurriculosProIA.Domain.Entities;
using CurriculosProIA.Domain.Dtos;
using CurriculosProIA.Repository.Persistence;
using CurriculosProIA.Domain.Dtos;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

using CurriculosProIA.Service.Interfaces;
using CurriculosProIA.Repository.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CurriculosProIA.Service.Implementations;

public class ResumeGeneratorService : IResumeGeneratorService
{
    private readonly IAiService _aiService;
    private readonly IJobSitesService _jobSites;

    public ResumeGeneratorService(IAiService aiService, IJobSitesService jobSites)
    {
        _aiService = aiService;
        _jobSites = jobSites;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<string> GenerateImprovedResumeAsync(
        string originalText,
        AnalysisInput analysis,
        string? siteId = null,
        CancellationToken cancellationToken = default)
    {
        var siteInfo = await BuildSiteInfoAsync(siteId, cancellationToken);
        var siteKeywords = new List<string>();

        if (!string.IsNullOrEmpty(siteId))
        {
            var site = await _jobSites.GetJobSiteByIdAsync(siteId, cancellationToken);
            siteKeywords = site?.PalavrasChavePadrao ?? new List<string>();
        }

        var pontosFortes = analysis.PontosFortes != null ? string.Join(", ", analysis.PontosFortes) : "Não especificado";
        var pontosMelhorar = analysis.PontosMelhorar != null ? string.Join(", ", analysis.PontosMelhorar) : "Não especificado";
        var recomendacoes = analysis.Recomendacoes != null ? string.Join("; ", analysis.Recomendacoes) : "Não especificado";

        var systemPrompt = $"""
            Você é um especialista em redação de currículos profissionais otimizados para ATS (Applicant Tracking Systems) e análise por IA de recrutadores.
            Sua função é reescrever e melhorar currículos aplicando as recomendações fornecidas, mantendo todas as informações verdadeiras e relevantes do currículo original.

            IMPORTANTE:
            - Mantenha TODAS as informações verdadeiras do currículo original
            - Aplique as melhorias sugeridas na análise
            - Melhore a formatação e organização
            - Use linguagem profissional e clara
            - Mantenha a estrutura padrão de currículo (Dados Pessoais, Objetivo, Experiência, Formação, Habilidades)
            - Não invente informações que não estavam no original
            - Otimize o currículo para passar por sistemas ATS e análise de IA
            {(siteKeywords.Count > 0 ? $"- Use naturalmente as seguintes palavras-chave estratégicas relevantes para o site: {string.Join(", ", siteKeywords)}" : "")}
            """;

        var userPrompt = $"""
            Com base no currículo original e na análise fornecida, gere uma versão melhorada do currículo.

            {siteInfo}

            CURRÍCULO ORIGINAL:
            {originalText}

            ANÁLISE E RECOMENDAÇÕES:
            - Pontos Fortes: {pontosFortes}
            - Pontos a Melhorar: {pontosMelhorar}
            - Recomendações: {recomendacoes}

            Gere um currículo melhorado que:
            1. Mantém todas as informações verdadeiras do original
            2. Aplica as recomendações da análise
            3. Melhora a organização e clareza
            4. Destaque os pontos fortes identificados
            5. Corrige ou melhora os pontos fracos mencionados
            {(siteKeywords.Count > 0 ? $"6. Incorpora naturalmente as palavras-chave estratégicas: {string.Join(", ", siteKeywords)}" : "")}
            8. É otimizado para passar por sistemas ATS e análise de IA de recrutadores

            Retorne APENAS o texto do currículo melhorado, sem explicações adicionais.
            """;

        var improved = await _aiService.GenerateTextAsync($"{systemPrompt}\n\n{userPrompt}", 0.7, 3000, cancellationToken);
        return AiService.CleanMarkdownFence(improved);
    }

    public byte[] GenerateResumePdf(string resumeText)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(50);
                page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Helvetica"));

                page.Content().Column(column =>
                {
                    column.Item().AlignCenter().Text("CURRÍCULO").Bold().FontSize(20);
                    column.Item().PaddingVertical(10);

                    foreach (var line in resumeText.Split('\n'))
                    {
                        var trimmed = line.Trim();
                        if (string.IsNullOrEmpty(trimmed))
                        {
                            column.Item().PaddingBottom(5);
                            continue;
                        }

                        var isHeader = trimmed.Length < 50 && (
                            trimmed == trimmed.ToUpperInvariant() ||
                            trimmed.Contains("---") ||
                            trimmed.Contains("==="));

                        if (isHeader)
                        {
                            column.Item().Text(trimmed.Replace("-", "").Replace("=", "")).Bold().FontSize(14);
                            column.Item().PaddingBottom(3);
                        }
                        else
                        {
                            column.Item().Text(trimmed);
                            column.Item().PaddingBottom(2);
                        }
                    }
                });
            });
        }).GeneratePdf();
    }

    private async Task<string> BuildSiteInfoAsync(string? siteId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(siteId))
        {
            return string.Empty;
        }

        var site = await _jobSites.GetJobSiteByIdAsync(siteId, cancellationToken);
        if (site == null)
        {
            return string.Empty;
        }

        var keywords = site.PalavrasChavePadrao ?? new List<string>();
        var characteristics = site.Caracteristicas != null
            ? JsonSerializer.Serialize(site.Caracteristicas, new JsonSerializerOptions { WriteIndented = true })
            : "{}";

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine("CONTEXTO CRÍTICO - SITE DE VAGAS SELECIONADO:");
        sb.AppendLine($"Este currículo será usado no site: {site.Nome}");
        if (!string.IsNullOrEmpty(site.Descricao))
        {
            sb.AppendLine($"Descrição do site: {site.Descricao}");
        }

        sb.AppendLine($"Características específicas do site: {characteristics}");
        if (keywords.Count > 0)
        {
            sb.AppendLine($"PALAVRAS-CHAVE PRIORITÁRIAS PARA ESTE SITE (ESSENCIAIS PARA ATS): {string.Join(", ", keywords)}");
        }

        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine();
        sb.AppendLine($"IMPORTANTE: Toda a geração DEVE ser adaptada especificamente para o site {site.Nome}.");
        return sb.ToString();
    }
}
