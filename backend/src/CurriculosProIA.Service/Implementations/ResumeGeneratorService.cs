using System.Text;
using System.Text.Json;
using CurriculosProIA.Domain.Dtos;

using CurriculosProIA.Service.Interfaces;

namespace CurriculosProIA.Service.Implementations;

public class ResumeGeneratorService : IResumeGeneratorService
{
    private const int MaxOutputTokens = 8192;
    private const float GenerationTemperature = 0.35f;

    private readonly IAiService _aiService;
    private readonly IJobSitesService _jobSites;
    private readonly IResumeKeywordService _keywordService;

    public ResumeGeneratorService(
        IAiService aiService,
        IJobSitesService jobSites,
        IResumeKeywordService keywordService)
    {
        _aiService = aiService;
        _jobSites = jobSites;
        _keywordService = keywordService;
    }

    public async Task<string> GenerateImprovedResumeAsync(
        string originalText,
        AnalysisInput analysis,
        string? siteId = null,
        string? candidateName = null,
        CancellationToken cancellationToken = default)
    {
        var site = !string.IsNullOrEmpty(siteId)
            ? await _jobSites.GetJobSiteByIdAsync(siteId, cancellationToken)
            : null;
        var siteInfo = BuildSiteInfoAsync(site);
        var identity = ResumeIdentityHelper.Extract(originalText);
        var keywords = await _keywordService.GenerateKeywordsAsync(originalText, analysis, site, cancellationToken);
        var platform = ResumePlatformTemplates.Resolve(site?.Nome);
        var identityBlock = ResumeIdentityHelper.BuildIdentityPromptBlock(identity, portuguese: true);
        var verifiedName = !string.IsNullOrWhiteSpace(identity.Name)
            ? identity.Name
            : candidateName?.Trim();

        var pontosFortes = analysis.PontosFortes != null ? string.Join(", ", analysis.PontosFortes) : "Não especificado";
        var pontosMelhorar = analysis.PontosMelhorar != null ? string.Join(", ", analysis.PontosMelhorar) : "Não especificado";
        var recomendacoes = analysis.Recomendacoes != null ? string.Join("; ", analysis.Recomendacoes) : "Não especificado";
        var habilidades = analysis.Habilidades != null ? string.Join(", ", analysis.Habilidades) : "Não especificado";
        var experienciaResumo = analysis.Experiencia ?? "Não especificado";
        var formacaoResumo = analysis.Formacao ?? "Não especificado";

        var candidateNameBlock = !string.IsNullOrWhiteSpace(verifiedName)
            ? $"""
            - Nome do candidato (OBRIGATÓRIO na 1ª linha de DADOS PESSOAIS, grafia idêntica): {verifiedName}
            - É PROIBIDO usar qualquer outro nome, inclusive de recrutador, exemplo ou usuário do sistema
            """
            : """
            - Extraia o nome EXATO do currículo original para a 1ª linha de DADOS PESSOAIS
            - Nunca invente ou substitua o nome do candidato
            """;

        var keywordBlock = keywords.Count > 0
            ? $"""
            PALAVRAS-CHAVE OBRIGATÓRIAS (incorporar naturalmente em RESUMO, EXPERIÊNCIA e HABILIDADES):
            {string.Join(", ", keywords)}
            """
            : string.Empty;

        var systemPrompt = $"""
            Você é um especialista sênior em redação de currículos otimizados para ATS, LinkedIn e recrutadores.
            Reescreva o currículo elevando o impacto profissional, mantendo 100% de fidelidade factual ao original.

            {identityBlock}

            REGRAS DE CONTEÚDO (CRÍTICAS):
            - Tom profissional, técnico e específico — sem clichês de RH ("proativo", "dinâmico", "busco desafios")
            - Cada bullet: verbo de ação no passado + tecnologia/ferramenta + entrega concreta
            - Use números, %, volumes e prazos SOMENTE se constarem no original; nunca invente métricas
            - Se não houver métrica no original, descreva escopo real (sistemas, integrações, volume de usuários mencionado)
            - Preserve TODAS as experiências, empresas, datas e certificações do original
            - Não invente e-mail, telefone, LinkedIn, GitHub, idioma, curso ou tecnologia
            - Não crie seção IDIOMAS se não existir no original
            - {candidateNameBlock}

            REGRAS DE FORMATO (OBRIGATÓRIAS):
            - Texto puro, sem markdown (sem ##, **, tabelas ou blocos de código)
            - Cabeçalhos de seção em MAIÚSCULAS, uma linha cada, exatamente como listado
            - Bullets SEMPRE com "- " no início (hífen + espaço)
            - Cargo/área técnica ficam em RESUMO PROFISSIONAL, nunca isolados no topo
            - Cada experiência: linha "Empresa | Cargo | Período" seguida de 3-5 bullets quando houver conteúdo
            - Não trunque descrições — complete todas as experiências do original
            - Ordem das seções: {platform.SectionOrderPt}

            ESTILO PARA O PORTAL:
            {platform.StyleInstructionsPt}

            FORMATO DE EXPERIÊNCIA:
            {platform.ExperienceFormatPt}

            {keywordBlock}
            """;

        var userPrompt = $"""
            {siteInfo}

            CURRÍCULO ORIGINAL:
            {originalText}

            ANÁLISE:
            - Pontos Fortes: {pontosFortes}
            - Pontos a Melhorar: {pontosMelhorar}
            - Recomendações: {recomendacoes}
            - Habilidades identificadas: {habilidades}
            - Resumo de experiência: {experienciaResumo}
            - Formação: {formacaoResumo}

            Gere o currículo melhorado aplicando as recomendações com ganho real de impacto e clareza.

            Formato obrigatório (início exato):
            DADOS PESSOAIS
            {(string.IsNullOrWhiteSpace(verifiedName) ? "[NOME COMPLETO DO ORIGINAL]" : verifiedName)}
            {(string.IsNullOrWhiteSpace(identity.ContactLine) ? "[contato do original — não inventar]" : identity.ContactLine)}

            RESUMO PROFISSIONAL
            [cargo/área + anos de experiência + stack principal + 1-2 conquistas reais do original]

            EXPERIÊNCIA PROFISSIONAL
            [cada experiência: Empresa | Cargo | Período + bullets com tecnologia e entrega concreta]

            FORMAÇÃO ACADÊMICA
            [curso | instituição | período]

            HABILIDADES TÉCNICAS
            [categorias: Linguagens, Frameworks, Bancos, Cloud — somente tecnologias do original]

            Retorne APENAS o texto do currículo, pronto para PDF/Word.
            """;

        var improved = await _aiService.GenerateTextAsync(
            $"{systemPrompt}\n\n{userPrompt}",
            GenerationTemperature,
            MaxOutputTokens,
            cancellationToken);
        var cleaned = NormalizeGeneratedText(AiService.CleanMarkdownFence(improved));
        return ResumeIdentityHelper.EnforceFidelity(cleaned, identity, originalText);
    }

    public async Task<string> GenerateEnglishResumeAsync(
        string originalText,
        AnalysisInput? analysis,
        string? siteId = null,
        string? candidateName = null,
        CancellationToken cancellationToken = default)
    {
        var site = !string.IsNullOrEmpty(siteId)
            ? await _jobSites.GetJobSiteByIdAsync(siteId, cancellationToken)
            : null;
        var siteInfo = BuildSiteInfoAsync(site);
        var identity = ResumeIdentityHelper.Extract(originalText);
        var identityBlock = ResumeIdentityHelper.BuildIdentityPromptBlock(identity, portuguese: false);
        var verifiedName = !string.IsNullOrWhiteSpace(identity.Name)
            ? identity.Name
            : candidateName?.Trim();
        var keywords = analysis != null
            ? await _keywordService.GenerateKeywordsAsync(originalText, analysis, site, cancellationToken)
            : Array.Empty<string>();
        var platform = ResumePlatformTemplates.Resolve(site?.Nome);

        var analysisBlock = string.Empty;
        if (analysis != null)
        {
            var pontosFortes = analysis.PontosFortes != null ? string.Join(", ", analysis.PontosFortes) : "Not specified";
            var pontosMelhorar = analysis.PontosMelhorar != null ? string.Join(", ", analysis.PontosMelhorar) : "Not specified";
            var recomendacoes = analysis.Recomendacoes != null ? string.Join("; ", analysis.Recomendacoes) : "Not specified";
            var habilidades = analysis.Habilidades != null ? string.Join(", ", analysis.Habilidades) : "Not specified";

            analysisBlock = $"""

                ANALYSIS CONTEXT:
                - Strengths: {pontosFortes}
                - Areas to improve: {pontosMelhorar}
                - Recommendations: {recomendacoes}
                - Skills: {habilidades}
                - Experience summary: {analysis.Experiencia ?? "Not specified"}
                - Education: {analysis.Formacao ?? "Not specified"}
                """;
        }

        var candidateNameBlock = !string.IsNullOrWhiteSpace(verifiedName)
            ? $"""
            - Candidate full name (MANDATORY first line under CONTACT, exact spelling): {verifiedName}
            - FORBIDDEN to use any other name, including recruiter, example or system user
            """
            : """
            - Extract the EXACT name from the source resume for the first line under CONTACT
            - Never invent or substitute the candidate name
            """;

        var keywordBlock = keywords.Count > 0
            ? $"""
            REQUIRED KEYWORDS (weave naturally into SUMMARY, EXPERIENCE and SKILLS):
            {string.Join(", ", keywords)}
            """
            : string.Empty;

        var systemPrompt = $"""
            You are a senior expert in ATS-optimized professional resume writing in English.
            Translate and adapt the resume with stronger professional impact while keeping 100% factual fidelity.

            {identityBlock}

            CONTENT RULES (CRITICAL):
            - Professional, technical, specific tone — no HR clichés ("proactive", "dynamic", "seeking challenges")
            - Each bullet: past-tense action verb + technology/tool + concrete deliverable
            - Use numbers, %, volume and timeframes ONLY if present in the source; never invent metrics
            - If no metric exists, describe real scope (systems, integrations, user volume mentioned)
            - Preserve ALL roles, employers, dates and certifications from the source
            - Do not invent email, phone, LinkedIn, GitHub, language, course or technology
            - Do not add a LANGUAGES section unless it exists in the source
            - {candidateNameBlock}

            FORMAT RULES (MANDATORY):
            - Plain text only, no markdown (no ##, **, tables or code blocks)
            - Section headers in UPPERCASE, one per line
            - Bullets ALWAYS start with "- " (hyphen + space)
            - Job title/headline goes in PROFESSIONAL SUMMARY
            - Each role: "Company | Role | Period" line followed by 3-5 bullets when content allows
            - Do not truncate descriptions — complete all experiences from the source
            - Section order: {platform.SectionOrderEn}

            PORTAL STYLE:
            {platform.StyleInstructionsEn}

            EXPERIENCE FORMAT:
            {platform.ExperienceFormatEn}

            {keywordBlock}
            """;

        var userPrompt = $"""
            Create a complete professional resume in English based on the source below.
            {siteInfo}
            {analysisBlock}

            SOURCE RESUME:
            {originalText}

            Required format (exact start):
            CONTACT
            {(string.IsNullOrWhiteSpace(verifiedName) ? "[FULL NAME FROM SOURCE]" : verifiedName)}
            {(string.IsNullOrWhiteSpace(identity.ContactLine) ? "[contact from source — do not invent]" : identity.ContactLine)}

            PROFESSIONAL SUMMARY
            [role/area + years of experience + main stack + 1-2 real achievements from source]

            PROFESSIONAL EXPERIENCE
            [each role: Company | Role | Period + bullets with technology and concrete delivery]

            EDUCATION
            [degree | institution | period]

            TECHNICAL SKILLS
            [categories: Languages, Frameworks, Databases, Cloud — only technologies from source]

            Return ONLY the resume text, ready for PDF/Word export.
            """;

        var englishResume = await _aiService.GenerateTextAsync(
            $"{systemPrompt}\n\n{userPrompt}",
            GenerationTemperature,
            MaxOutputTokens,
            cancellationToken);
        var cleaned = NormalizeGeneratedText(AiService.CleanMarkdownFence(englishResume));
        return ResumeIdentityHelper.EnforceFidelity(cleaned, identity, originalText);
    }

    public byte[] GenerateResumeExcel(string resumeText) =>
        ResumeExcelBuilder.BuildFromText(resumeText);

    public byte[] GenerateResumePdf(string resumeText, string? candidateName = null)
    {
        var layout = ResumeLayoutHelper.Parse(resumeText, candidateName);
        return ResumePdfRenderer.Render(layout);
    }

    public byte[] GenerateResumeDocx(string resumeText, string? candidateName = null) =>
        ResumeDocxBuilder.BuildFromText(resumeText, candidateName);

    private static string BuildSiteInfoAsync(CurriculosProIA.Repository.Persistence.SiteVagasRow? site)
    {
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
        sb.AppendLine($"Portal: {site.Nome}");
        if (!string.IsNullOrEmpty(site.Descricao))
        {
            sb.AppendLine($"Descrição: {site.Descricao}");
        }

        sb.AppendLine($"Características: {characteristics}");
        if (keywords.Count > 0)
        {
            sb.AppendLine($"Keywords base do portal: {string.Join(", ", keywords)}");
        }

        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        return sb.ToString();
    }

    private static string NormalizeGeneratedText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var lines = text
            .Replace("\r", string.Empty)
            .Split('\n')
            .Select(l => l.TrimEnd())
            .ToList();

        var normalized = new List<string>();
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                if (normalized.Count > 0 && string.IsNullOrWhiteSpace(normalized[^1]))
                {
                    continue;
                }

                normalized.Add(string.Empty);
                continue;
            }

            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                continue;
            }

            normalized.Add(trimmed);
        }

        while (normalized.Count > 0 && string.IsNullOrWhiteSpace(normalized[0]))
        {
            normalized.RemoveAt(0);
        }

        while (normalized.Count > 0 && string.IsNullOrWhiteSpace(normalized[^1]))
        {
            normalized.RemoveAt(normalized.Count - 1);
        }

        return string.Join('\n', normalized);
    }
}
