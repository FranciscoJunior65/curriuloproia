using System.Text;
using System.Text.Json;
using CurriculosProIA.Domain.Dtos;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

using CurriculosProIA.Service.Interfaces;
using CurriculosProIA.Repository.Interfaces;

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
        string? candidateName = null,
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
        var candidateNameBlock = !string.IsNullOrWhiteSpace(candidateName)
            ? $"""
            - O nome completo do candidato é: {candidateName.Trim()}
            - Use EXATAMENTE esse nome em DADOS PESSOAIS (primeira linha após o cabeçalho da seção)
            """
            : string.Empty;

        var systemPrompt = $"""
            Você é um especialista em redação de currículos profissionais otimizados para ATS (Applicant Tracking Systems) e análise por IA de recrutadores.
            Sua função é reescrever e melhorar currículos aplicando as recomendações fornecidas, mantendo todas as informações verdadeiras e relevantes do currículo original.

            IMPORTANTE:
            - Mantenha TODAS as informações verdadeiras do currículo original
            - Aplique as melhorias sugeridas na análise
            - Melhore a formatação e organização
            - Use linguagem profissional e clara
            - Mantenha a estrutura padrão de currículo (Dados Pessoais, Objetivo, Experiência, Formação, Habilidades)
            - Cabeçalhos de seção em MAIÚSCULAS, uma linha cada
            - Use linhas com "- " para conquistas e responsabilidades
            - NÃO coloque cargo, área de atuação ou headline antes do nome
            - Em DADOS PESSOAIS, a primeira linha deve ser SOMENTE o nome completo do candidato
            - Cargo, área técnica ou headline profissional vão em OBJETIVO PROFISSIONAL ou RESUMO PROFISSIONAL, nunca no topo isolado
            {candidateNameBlock}
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

            Formato obrigatório no início:
            DADOS PESSOAIS
            {(string.IsNullOrWhiteSpace(candidateName) ? "[NOME COMPLETO — apenas o nome, sem cargo]" : candidateName.Trim())}
            [cidade | telefone | e-mail | linkedin]

            RESUMO PROFISSIONAL
            [cargo/área de atuação e resumo aqui]

            Retorne APENAS o texto do currículo melhorado, linha a linha, com cabeçalhos de seção e bullets — pronto para exportação PDF/Word.
            """;

        var improved = await _aiService.GenerateTextAsync($"{systemPrompt}\n\n{userPrompt}", 0.7, 3000, cancellationToken);
        return AiService.CleanMarkdownFence(improved);
    }

    public async Task<string> GenerateEnglishResumeAsync(
        string originalText,
        AnalysisInput? analysis,
        string? siteId = null,
        string? candidateName = null,
        CancellationToken cancellationToken = default)
    {
        var siteInfo = await BuildSiteInfoAsync(siteId, cancellationToken);
        var analysisBlock = string.Empty;

        if (analysis != null)
        {
            var pontosFortes = analysis.PontosFortes != null ? string.Join(", ", analysis.PontosFortes) : "Not specified";
            var pontosMelhorar = analysis.PontosMelhorar != null ? string.Join(", ", analysis.PontosMelhorar) : "Not specified";
            var recomendacoes = analysis.Recomendacoes != null ? string.Join("; ", analysis.Recomendacoes) : "Not specified";

            analysisBlock = $"""

                ANALYSIS CONTEXT (use to improve the English version):
                - Strengths: {pontosFortes}
                - Areas to improve: {pontosMelhorar}
                - Recommendations: {recomendacoes}
                """;
        }

        var candidateNameBlock = !string.IsNullOrWhiteSpace(candidateName)
            ? $"""

            CANDIDATE FULL NAME (mandatory in CONTACT section): {candidateName.Trim()}
            """
            : string.Empty;

        var systemPrompt = $"""
            You are an expert in professional resume writing in English for international job markets and ATS systems.
            Translate and adapt the resume to fluent, professional English. Keep all factual information from the original.
            Do not invent experience, education, or skills that are not supported by the source text.
            Use the same professional structure as an improved resume: CONTACT, PROFESSIONAL SUMMARY, EXPERIENCE, EDUCATION, SKILLS (and others as needed).
            Section headers must be in English, one per line, in UPPERCASE.
            Use bullet lines starting with "- " for achievements and responsibilities.
            Do NOT put job title or professional headline before the candidate name.
            Under CONTACT, the first line must be ONLY the full name. Job title/headline goes in PROFESSIONAL SUMMARY.
            {candidateNameBlock}
            """;

        var userPrompt = $"""
            Create a complete professional resume in English based on the source below.
            Apply analysis recommendations when translating (highlight strengths, address weaknesses).
            {siteInfo}
            {analysisBlock}

            SOURCE RESUME:
            {originalText}

            Required format at the top:
            CONTACT
            {(string.IsNullOrWhiteSpace(candidateName) ? "[FULL NAME — name only, no job title]" : candidateName.Trim())}
            [city | phone | email | linkedin]

            PROFESSIONAL SUMMARY
            [job title/headline and summary here]

            Return ONLY the resume text in English, line by line, with section headers and bullets — ready for PDF/Word export (not a table or spreadsheet).
            """;

        var englishResume = await _aiService.GenerateTextAsync($"{systemPrompt}\n\n{userPrompt}", 0.7, 3000, cancellationToken);
        return AiService.CleanMarkdownFence(englishResume);
    }

    public byte[] GenerateResumeExcel(string resumeText) =>
        ResumeExcelBuilder.BuildFromText(resumeText);

    public byte[] GenerateResumePdf(string resumeText, string? candidateName = null)
    {
        var layout = ResumeLayoutHelper.Parse(resumeText, candidateName);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(10.5f).FontFamily("Helvetica").FontColor(Colors.Grey.Darken3));

                page.Content().Column(column =>
                {
                    column.Spacing(5);
                    column.Item().Text(layout.Name).FontSize(21).SemiBold().FontColor(Colors.Blue.Darken3);
                    if (!string.IsNullOrWhiteSpace(layout.Contact))
                    {
                        column.Item().Text(layout.Contact).FontSize(9.5f).FontColor(Colors.Grey.Darken1);
                    }
                    column.Item().PaddingTop(8).PaddingBottom(2).LineHorizontal(1).LineColor(Colors.Blue.Lighten3);

                    foreach (var section in layout.Sections)
                    {
                        if (!string.IsNullOrWhiteSpace(section.Title))
                        {
                            column.Item().PaddingTop(7).Element(x =>
                            {
                                x.Background(Colors.Blue.Lighten5)
                                 .Border(1)
                                 .BorderColor(Colors.Blue.Lighten3)
                                 .PaddingVertical(4)
                                 .PaddingHorizontal(8)
                                 .Text(section.Title.ToUpperInvariant())
                                 .FontSize(10)
                                 .SemiBold()
                                 .FontColor(Colors.Blue.Darken2);
                            });
                        }

                        foreach (var line in section.Lines)
                        {
                            if (string.IsNullOrWhiteSpace(line))
                            {
                                continue;
                            }

                            if (ResumeLayoutHelper.IsBulletLine(line))
                            {
                                var bulletText = ResumeLayoutHelper.StripBulletPrefix(line);
                                column.Item().PaddingBottom(2).Row(row =>
                                {
                                    row.Spacing(6);
                                    row.ConstantItem(8).Text("•").FontSize(11).FontColor(Colors.Blue.Medium);
                                    row.RelativeItem().Text(bulletText).LineHeight(1.35f);
                                });
                            }
                            else
                            {
                                column.Item().PaddingBottom(2).Text(line).LineHeight(1.35f);
                            }
                        }
                    }
                });
            });
        }).GeneratePdf();
    }

    public byte[] GenerateResumeDocx(string resumeText, string? candidateName = null) =>
        ResumeDocxBuilder.BuildFromText(resumeText, candidateName);

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
