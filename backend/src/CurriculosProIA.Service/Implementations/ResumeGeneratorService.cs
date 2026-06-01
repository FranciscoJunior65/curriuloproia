using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CurriculosProIA.Domain.Dtos;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

using CurriculosProIA.Service.Interfaces;
using CurriculosProIA.Repository.Interfaces;

namespace CurriculosProIA.Service.Implementations;

public class ResumeGeneratorService : IResumeGeneratorService
{
    private sealed record ResumeSection(string Title, List<string> Lines);
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

    public async Task<string> GenerateEnglishResumeAsync(
        string originalText,
        AnalysisInput? analysis,
        string? siteId = null,
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

        var systemPrompt = """
            You are an expert in professional resume writing in English for international job markets and ATS systems.
            Translate and adapt the resume to fluent, professional English. Keep all factual information from the original.
            Do not invent experience, education, or skills that are not supported by the source text.
            Use the same professional structure as an improved resume: CONTACT, PROFESSIONAL SUMMARY, EXPERIENCE, EDUCATION, SKILLS (and others as needed).
            Section headers must be in English, one per line, in UPPERCASE.
            Use bullet lines starting with "- " for achievements and responsibilities.
            """;

        var userPrompt = $"""
            Create a complete professional resume in English based on the source below.
            Apply analysis recommendations when translating (highlight strengths, address weaknesses).
            {siteInfo}
            {analysisBlock}

            SOURCE RESUME:
            {originalText}

            Return ONLY the resume text in English, line by line, with section headers and bullets — ready for PDF/Word export (not a table or spreadsheet).
            """;

        var englishResume = await _aiService.GenerateTextAsync($"{systemPrompt}\n\n{userPrompt}", 0.7, 3000, cancellationToken);
        return AiService.CleanMarkdownFence(englishResume);
    }

    public byte[] GenerateResumeExcel(string resumeText) =>
        ResumeExcelBuilder.BuildFromText(resumeText);

    public byte[] GenerateResumePdf(string resumeText)
    {
        var lines = NormalizeResumeLines(resumeText);
        var profile = ExtractProfile(lines);
        var sections = BuildSections(lines, profile);

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
                    column.Item().Text(profile.Name).FontSize(21).SemiBold().FontColor(Colors.Blue.Darken3);
                    if (!string.IsNullOrWhiteSpace(profile.Contact))
                    {
                        column.Item().Text(profile.Contact).FontSize(9.5f).FontColor(Colors.Grey.Darken1);
                    }
                    column.Item().PaddingTop(8).PaddingBottom(2).LineHorizontal(1).LineColor(Colors.Blue.Lighten3);

                    foreach (var section in sections)
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
                                continue;

                            if (IsBullet(line))
                            {
                                var bulletText = line.TrimStart('-', '*', '•', ' ').Trim();
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

    public byte[] GenerateResumeDocx(string resumeText) =>
        ResumeDocxBuilder.BuildFromText(resumeText);

    private static List<string> NormalizeResumeLines(string resumeText)
    {
        return (resumeText ?? string.Empty)
            .Replace("\r", string.Empty)
            .Split('\n')
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }

    private static (string Name, string Contact) ExtractProfile(List<string> lines)
    {
        var name = lines
            .Select(StripMarkdown)
            .FirstOrDefault(l =>
                !string.IsNullOrWhiteSpace(l) &&
                !IsLikelySectionTitle(l) &&
                l.Length <= 70 &&
                Regex.IsMatch(l, @"^[\p{L}\s\.'\-]+$", RegexOptions.CultureInvariant)) ?? "Currículo Profissional";

        var contact = lines
            .Select(StripMarkdown)
            .FirstOrDefault(l =>
                l.Contains("@", StringComparison.OrdinalIgnoreCase) ||
                l.Contains("|", StringComparison.OrdinalIgnoreCase) ||
                Regex.IsMatch(l, @"\(\d{2}\)|\d{8,}", RegexOptions.CultureInvariant)) ?? string.Empty;

        return (name, contact);
    }

    private static string StripMarkdown(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var cleaned = text.Trim();
        cleaned = cleaned.Replace("**", string.Empty).Replace("__", string.Empty);
        cleaned = Regex.Replace(cleaned, @"^\*\s*", string.Empty);
        cleaned = Regex.Replace(cleaned, @"^\-\s*", "- ");
        cleaned = Regex.Replace(cleaned, @"(?<!\*)\*(?!\*)", string.Empty);
        cleaned = Regex.Replace(cleaned, @"\[(.*?)\]\((.*?)\)", "$1");
        cleaned = Regex.Replace(cleaned, @"\s{2,}", " ");
        return cleaned.Trim();
    }

    private static bool IsLikelySectionTitle(string line)
    {
        var candidate = StripMarkdown(line).Trim(':', ' ', '-');
        if (string.IsNullOrWhiteSpace(candidate) || candidate.Length > 42)
            return false;

        var normalized = candidate.ToUpperInvariant();
        return candidate == normalized ||
               normalized.Contains("RESUMO") ||
               normalized.Contains("EXPERI") ||
               normalized.Contains("FORMA") ||
               normalized.Contains("HABIL") ||
               normalized.Contains("IDIOMA") ||
               normalized.Contains("OBJETIVO") ||
               normalized.Contains("INFORMA") ||
               normalized.Contains("SUMMARY") ||
               normalized.Contains("EXPERIENCE") ||
               normalized.Contains("EDUCATION") ||
               normalized.Contains("SKILLS") ||
               normalized.Contains("CONTACT") ||
               normalized.Contains("PROFILE");
    }

    private static bool IsBullet(string line)
    {
        var t = line.TrimStart();
        return t.StartsWith("- ") || t.StartsWith("* ") || t.StartsWith("• ");
    }

    private static List<ResumeSection> BuildSections(List<string> lines, (string Name, string Contact) profile)
    {
        var sections = new List<ResumeSection>();
        var currentTitle = "Resumo";
        var currentLines = new List<string>();

        foreach (var raw in lines)
        {
            var line = StripMarkdown(raw);
            if (string.IsNullOrWhiteSpace(line) ||
                line.Equals(profile.Name, StringComparison.OrdinalIgnoreCase) ||
                line.Equals(profile.Contact, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (IsLikelySectionTitle(line))
            {
                if (currentLines.Count > 0)
                {
                    sections.Add(new ResumeSection(currentTitle, currentLines));
                }

                currentTitle = line.Trim(':', ' ');
                currentLines = new List<string>();
                continue;
            }

            currentLines.Add(line);
        }

        if (currentLines.Count > 0)
        {
            sections.Add(new ResumeSection(currentTitle, currentLines));
        }

        return sections;
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
