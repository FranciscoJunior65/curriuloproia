using System.Globalization;
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

public class CoverLetterService : ICoverLetterService
{
    private readonly IAiService _aiService;
    private readonly IJobSitesService _jobSites;

    public CoverLetterService(IAiService aiService, IJobSitesService jobSites)
    {
        _aiService = aiService;
        _jobSites = jobSites;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<string> GenerateCoverLetterAsync(
        string resumeText,
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

        var habilidades = analysis.Habilidades != null ? string.Join(", ", analysis.Habilidades) : "Não especificado";
        var pontosFortes = analysis.PontosFortes != null ? string.Join(", ", analysis.PontosFortes) : "Não especificado";

        var systemPrompt = $"""
            Você é um especialista em redação de cartas de apresentação profissionais otimizadas para análise por sistemas ATS (Applicant Tracking Systems) e IAs de validação de currículo.
            Sua função é criar cartas de apresentação personalizadas, persuasivas e profissionais que destaquem as qualificações do candidato de forma estratégica e otimizada para passar por sistemas automatizados de triagem.

            IMPORTANTE:
            - A carta deve ser profissional, concisa e impactante
            - Destaque os pontos fortes identificados na análise
            - Use linguagem adequada ao contexto do site de vagas (se fornecido)
            - A carta deve ter entre 3-4 parágrafos
            - Seja específico e evite clichês genéricos
            - Mencione conquistas e resultados quando possível
            - Otimize a carta para passar por sistemas ATS e análise de IA de recrutadores
            {(siteKeywords.Count > 0 ? $"- Use naturalmente e estrategicamente as seguintes palavras-chave CRÍTICAS para o site: {string.Join(", ", siteKeywords)}" : "")}
            """;

        var userPrompt = $"""
            Com base no currículo e na análise fornecidos, crie uma carta de apresentação profissional e personalizada.

            {siteInfo}

            CURRÍCULO DO CANDIDATO:
            {resumeText}

            ANÁLISE DO CURRÍCULO:
            - Pontos Fortes: {pontosFortes}
            - Experiência: {analysis.Experiencia ?? "Não especificado"}
            - Formação: {analysis.Formacao ?? "Não especificado"}
            - Habilidades: {habilidades}
            - Score: {analysis.Score?.ToString() ?? "N/A"}/100

            Crie uma carta de apresentação que:
            1. Apresenta o candidato de forma profissional
            2. Destaca os principais pontos fortes e experiências relevantes
            3. Demonstra interesse e adequação para oportunidades
            4. Usa linguagem persuasiva mas profissional
            5. É específica e evita generalidades
            {(siteKeywords.Count > 0 ? $"6. Incorpora NATURALMENTE e ESTRATEGICAMENTE as palavras-chave CRÍTICAS: {string.Join(", ", siteKeywords)}" : "")}
            7. Está otimizada para passar por sistemas ATS e análise de IA de recrutadores

            Formato da carta:
            - Saudação profissional
            - Parágrafo introdutório: Apresentação e objetivo
            - Parágrafo(s) do meio: Destaque de qualificações e experiências relevantes
            - Parágrafo final: Encerramento profissional e disponibilidade para contato

            Retorne APENAS o texto da carta de apresentação, sem explicações adicionais.
            """;

        var letter = await _aiService.GenerateTextAsync($"{systemPrompt}\n\n{userPrompt}", 0.8, 1500, cancellationToken);
        return AiService.CleanMarkdownFence(letter);
    }

    public byte[] GenerateCoverLetterPdf(string coverLetterText)
    {
        var dateStr = DateTime.Now.ToString("dd 'de' MMMM 'de' yyyy", new CultureInfo("pt-BR"));
        var paragraphs = coverLetterText
            .Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrEmpty(p))
            .ToList();

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(50);
                page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Helvetica"));

                page.Content().Column(column =>
                {
                    column.Item().AlignCenter().Text("CARTA DE APRESENTAÇÃO").Bold().FontSize(18);
                    column.Item().PaddingVertical(15);
                    column.Item().AlignRight().Text(dateStr).FontSize(10);
                    column.Item().PaddingVertical(10);

                    for (var i = 0; i < paragraphs.Count; i++)
                    {
                        column.Item().Text(paragraphs[i]).Justify();
                        column.Item().PaddingBottom(10);
                    }

                    column.Item().PaddingTop(20).Text("Atenciosamente,");
                    column.Item().PaddingTop(15).Text("___________________________");
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
        sb.AppendLine($"Esta carta será usada no site: {site.Nome}");
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
        return sb.ToString();
    }
}
