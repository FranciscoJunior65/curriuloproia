namespace CurriculosProIA.Service.Implementations;

/// <summary>Instruções de estilo e estrutura por portal de vagas para geração de currículo.</summary>
public sealed record ResumePlatformProfile(
    string SectionOrderPt,
    string SectionOrderEn,
    string StyleInstructionsPt,
    string StyleInstructionsEn,
    string ExperienceFormatPt,
    string ExperienceFormatEn);

public static class ResumePlatformTemplates
{
    private static readonly ResumePlatformProfile Default = new(
        SectionOrderPt: "DADOS PESSOAIS → RESUMO PROFISSIONAL → EXPERIÊNCIA PROFISSIONAL → FORMAÇÃO ACADÊMICA → HABILIDADES TÉCNICAS → IDIOMAS → CERTIFICAÇÕES (se houver)",
        SectionOrderEn: "CONTACT → PROFESSIONAL SUMMARY → PROFESSIONAL EXPERIENCE → EDUCATION → TECHNICAL SKILLS → LANGUAGES → CERTIFICATIONS (if any)",
        StyleInstructionsPt: """
            - Linguagem objetiva, técnica e profissional
            - Destaque tecnologias, ferramentas e resultados mensuráveis quando existirem no original
            - Bullets com verbos de ação no passado (desenvolveu, implementou, otimizou)
            - Cada bullet deve citar pelo menos uma tecnologia ou entrega concreta do original
            - Evite frases genéricas como "boa comunicação" sem contexto técnico
            """,
        StyleInstructionsEn: """
            - Objective, technical and professional language
            - Highlight technologies, tools and measurable outcomes
            - Bullets with strong action verbs (developed, implemented, optimized)
            - Avoid generic phrases without technical context
            """,
        ExperienceFormatPt: """
            Empresa | Cargo | MM/AAAA – MM/AAAA (ou Atual)
            - Conquista ou responsabilidade com tecnologia e resultado quantificado
            """,
        ExperienceFormatEn: """
            Company | Role | MM/YYYY – MM/YYYY (or Present)
            - Achievement or responsibility with technology and quantified result
            """);

    private static readonly Dictionary<string, ResumePlatformProfile> BySiteName =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["LinkedIn"] = Default with
            {
                StyleInstructionsPt = """
                    - Tom profissional com narrativa de carreira (progressão técnica e impacto)
                    - Resumo em 3-4 linhas: cargo-alvo + anos de experiência + stack principal + conquista real
                    - Cada experiência: contexto breve → ação com tecnologias → resultado concreto (métrica só se existir no original)
                    - Destaque liderança, mentoria ou coordenação SOMENTE se constar no original
                    - Palavras-chave de networking e colaboração quando suportadas pelo perfil real
                    - Evite buzzwords vazias ("inovação", "alto impacto") sem prova no texto original
                    """,
                StyleInstructionsEn = """
                    - Professional tone with career narrative (technical progression and impact)
                    - Summary in 3-4 lines: target role + years of experience + main stack + real achievement
                    - Each role: brief context → action with technologies → concrete outcome (metric only if in source)
                    - Highlight leadership or mentoring ONLY if present in the source
                    - Networking and collaboration keywords when supported by the real profile
                    - Avoid empty buzzwords ("innovation", "high impact") without proof in the source
                    """
            },
            ["Gupy"] = Default with
            {
                StyleInstructionsPt = """
                    - Formato ATS-friendly: seções claras, sem tabelas ou colunas
                    - Priorize palavras-chave técnicas do currículo original em HABILIDADES e EXPERIÊNCIA
                    - Cada bullet deve conter tecnologia + resultado mensurável (%, tempo, volume)
                    - Resumo curto e direto (2-3 linhas) com stack principal
                    """,
                StyleInstructionsEn = """
                    - ATS-friendly format: clear sections, no tables or columns
                    - Prioritize technical keywords from the source in SKILLS and EXPERIENCE
                    - Each bullet must include technology + measurable result (%, time, volume)
                    - Short summary (2-3 lines) with main tech stack
                    """
            },
            ["Indeed"] = Default with
            {
                StyleInstructionsPt = """
                    - Otimizado para ATS e busca por keywords
                    - Liste habilidades técnicas em categorias (Linguagens, Frameworks, Ferramentas)
                    - Repita termos relevantes do setor de forma natural nas experiências
                    - Bullets objetivos com métricas sempre que possível
                    """,
                StyleInstructionsEn = """
                    - Optimized for ATS and keyword search
                    - List technical skills by category (Languages, Frameworks, Tools)
                    - Repeat relevant industry terms naturally in experience bullets
                    - Objective bullets with metrics whenever possible
                    """
            },
            ["InfoJobs"] = Default with
            {
                StyleInstructionsPt = """
                    - Estrutura tradicional e formal exigida pelo portal
                    - Destaque formação acadêmica e qualificações após o resumo
                    - Experiências em ordem cronológica reversa com datas explícitas
                    - Habilidades separadas em técnicas e comportamentais (somente as do original)
                    """,
                StyleInstructionsEn = """
                    - Traditional formal structure expected by the portal
                    - Highlight education and qualifications after summary
                    - Experience in reverse chronological order with explicit dates
                    - Skills split into technical and soft (only from the source)
                    """
            },
            ["Vagas.com"] = Default with
            {
                StyleInstructionsPt = """
                    - Descrições detalhadas mas objetivas em cada experiência
                    - Mínimo 3 bullets por experiência relevante quando houver conteúdo no original
                    - Objetivo profissional claro no resumo com área de atuação
                    """,
                StyleInstructionsEn = """
                    - Detailed but objective descriptions per role
                    - Minimum 3 bullets per relevant role when source content allows
                    - Clear professional objective in summary with area of expertise
                    """
            },
            ["Catho"] = Default with
            {
                StyleInstructionsPt = """
                    - Objetivo profissional explícito no resumo (cargo-alvo)
                    - Competências e realizações em destaque
                    - Formato tradicional brasileiro com clareza e objetividade
                    """,
                StyleInstructionsEn = """
                    - Explicit career objective in summary (target role)
                    - Competencies and achievements highlighted
                    - Traditional Brazilian format with clarity and objectivity
                    """
            },
            ["Trabalhar Brasil"] = Default with
            {
                StyleInstructionsPt = """
                    - Clareza e objetividade para mercado brasileiro amplo
                    - Informações de disponibilidade e localização quando presentes no original
                    - Linguagem acessível mantendo termos técnicos corretos
                    """,
                StyleInstructionsEn = """
                    - Clarity and objectivity for the broad Brazilian market
                    - Availability and location when present in the source
                    - Accessible language with correct technical terms
                    """
            },
            ["Empregos.com.br"] = Default with
            {
                StyleInstructionsPt = """
                    - Equilíbrio entre experiência quantificada e competências
                    - Seções bem delimitadas para facilitar leitura por recrutadores
                    """,
                StyleInstructionsEn = """
                    - Balance between quantified experience and competencies
                    - Well-delimited sections for recruiter readability
                    """
            },
            ["Google Vagas"] = Default with
            {
                StyleInstructionsPt = """
                    - Máxima compatibilidade ATS (agregador multi-portais)
                    - Keywords técnicas distribuídas em resumo, experiência e habilidades
                    - Formato limpo sem elementos gráficos ou símbolos especiais
                    """,
                StyleInstructionsEn = """
                    - Maximum ATS compatibility (multi-portal aggregator)
                    - Technical keywords distributed across summary, experience and skills
                    - Clean format without graphics or special symbols
                    """
            }
        };

    public static ResumePlatformProfile Resolve(string? siteName) =>
        !string.IsNullOrWhiteSpace(siteName) && BySiteName.TryGetValue(siteName.Trim(), out var profile)
            ? profile
            : Default;
}
