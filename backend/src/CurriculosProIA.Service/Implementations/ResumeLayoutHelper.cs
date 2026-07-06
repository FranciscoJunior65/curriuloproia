using System.Text.RegularExpressions;

namespace CurriculosProIA.Service.Implementations;

public sealed record ResumeSectionBlock(string Title, List<string> Lines);

public sealed record ResumeLayout(string Name, string Contact, List<ResumeSectionBlock> Sections);

/// <summary>Parser compartilhado entre exportação PDF e Word (PT/EN).</summary>
public static class ResumeLayoutHelper
{
    private static readonly string[] KnownSectionTitles =
    [
        "DADOS PESSOAIS", "INFORMAÇÕES PESSOAIS", "INFORMACOES PESSOAIS", "CONTACT", "CONTATO",
        "RESUMO PROFISSIONAL", "OBJETIVO PROFISSIONAL", "PROFESSIONAL SUMMARY", "SUMMARY",
        "EXPERIÊNCIA PROFISSIONAL", "EXPERIENCIA PROFISSIONAL", "EXPERIÊNCIA", "EXPERIENCIA",
        "PROFESSIONAL EXPERIENCE", "EXPERIENCE", "WORK EXPERIENCE",
        "FORMAÇÃO ACADÊMICA", "FORMACAO ACADEMICA", "FORMAÇÃO", "FORMACAO", "EDUCATION",
        "HABILIDADES TÉCNICAS", "HABILIDADES TECNICAS", "HABILIDADES", "COMPETÊNCIAS", "COMPETENCIAS",
        "TECHNICAL SKILLS", "SKILLS", "CORE COMPETENCIES",
        "IDIOMAS", "LANGUAGES", "CERTIFICAÇÕES", "CERTIFICACOES", "CERTIFICATIONS",
        "CURSOS", "PROJETOS", "PROJECTS", "INFORMAÇÕES ADICIONAIS", "INFORMACOES ADICIONAIS",
        "ADDITIONAL INFORMATION", "PROFILE"
    ];

    private static readonly string[] ProfileSectionMarkers =
    [
        "DADOS PESSOAIS",
        "INFORMAÇÕES PESSOAIS",
        "INFORMACOES PESSOAIS",
        "CONTACT",
        "CONTATO"
    ];

    public static ResumeLayout Parse(string resumeText, string? candidateName = null)
    {
        var lines = NormalizeResumeLines(resumeText);
        var profile = ExtractProfile(lines);
        var name = !string.IsNullOrWhiteSpace(candidateName)
            ? candidateName.Trim()
            : profile.Name;
        var sections = BuildSections(lines, (name, profile.Contact))
            .Where(s => s.Lines.Count > 0)
            .ToList();
        return new ResumeLayout(name, profile.Contact, sections);
    }

    public static bool IsBulletLine(string line)
    {
        var t = line.TrimStart();
        return t.StartsWith("- ", StringComparison.Ordinal) ||
               t.StartsWith("* ", StringComparison.Ordinal) ||
               t.StartsWith("• ", StringComparison.Ordinal) ||
               Regex.IsMatch(t, @"^\d+[\.\)]\s+", RegexOptions.CultureInvariant);
    }

    public static string StripBulletPrefix(string line)
    {
        var t = line.TrimStart();
        if (t.StartsWith("- ", StringComparison.Ordinal) ||
            t.StartsWith("* ", StringComparison.Ordinal) ||
            t.StartsWith("• ", StringComparison.Ordinal))
        {
            return t[2..].Trim();
        }

        var numbered = Regex.Match(t, @"^\d+[\.\)]\s+(.*)$", RegexOptions.CultureInvariant);
        return numbered.Success ? numbered.Groups[1].Value.Trim() : t.TrimStart('-', '*', '•', ' ').Trim();
    }

    public static bool IsExperienceHeaderLine(string line)
    {
        var candidate = StripMarkdown(line).Trim();
        if (string.IsNullOrWhiteSpace(candidate) || IsBulletLine(candidate))
        {
            return false;
        }

        if (IsKnownSectionHeader(candidate))
        {
            return false;
        }

        var hasSeparator = candidate.Contains('|', StringComparison.Ordinal) ||
                           candidate.Contains('—', StringComparison.Ordinal) ||
                           candidate.Contains(" - ", StringComparison.Ordinal);
        var hasDate = Regex.IsMatch(candidate, @"\b\d{2}/\d{4}\b|\b\d{4}\b|\bAtual\b|\bPresent\b", RegexOptions.IgnoreCase);

        return hasSeparator || (hasDate && candidate.Length <= 120);
    }

    private static List<string> NormalizeResumeLines(string resumeText)
    {
        return (resumeText ?? string.Empty)
            .Replace("\r", string.Empty)
            .Split('\n')
            .Select(StripMarkdown)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }

    private static (string Name, string Contact) ExtractProfile(List<string> lines)
    {
        var name =
            FindNameAfterProfileSection(lines) ??
            lines.FirstOrDefault(l => IsLikelyPersonName(l)) ??
            lines
                .Where(l => IsLikelyPersonName(l))
                .OrderByDescending(ScorePersonName)
                .FirstOrDefault() ??
            "Currículo Profissional";

        var contact = lines
            .FirstOrDefault(IsContactLine) ?? string.Empty;

        return (name, contact);
    }

    private static string? FindNameAfterProfileSection(IReadOnlyList<string> lines)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            if (!IsProfileSectionTitle(lines[i]))
            {
                continue;
            }

            for (var j = i + 1; j < Math.Min(i + 5, lines.Count); j++)
            {
                if (IsLikelySectionTitle(lines[j]))
                {
                    break;
                }

                if (IsContactLine(lines[j]))
                {
                    continue;
                }

                if (IsLikelyPersonName(lines[j], strictHeadlineCheck: false))
                {
                    return lines[j];
                }
            }
        }

        return null;
    }

    private static bool IsProfileSectionTitle(string line)
    {
        var normalized = StripMarkdown(line).Trim(':', ' ', '-').ToUpperInvariant();
        return ProfileSectionMarkers.Any(marker => normalized.Contains(marker, StringComparison.Ordinal));
    }

    private static bool IsContactLine(string line)
    {
        var candidate = StripMarkdown(line).Trim();
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        return candidate.Contains('@', StringComparison.OrdinalIgnoreCase) ||
               candidate.Contains('|', StringComparison.Ordinal) ||
               candidate.Contains("linkedin", StringComparison.OrdinalIgnoreCase) ||
               Regex.IsMatch(candidate, @"\(\d{2}\)|\d{8,}", RegexOptions.CultureInvariant);
    }

    public static bool IsContactLinePublic(string line) => IsContactLine(line);

    public static bool IsLikelyPersonNamePublic(string line) =>
        IsLikelyPersonName(line, strictHeadlineCheck: false);

    public static bool IsKnownSectionHeaderPublic(string line) => IsKnownSectionHeader(line);

    private static bool IsLikelyPersonName(string line, bool strictHeadlineCheck = true)
    {
        var candidate = StripMarkdown(line).Trim();
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        if (IsKnownSectionHeader(candidate))
        {
            return false;
        }

        if (IsBulletLine(candidate) || candidate.StartsWith('-'))
        {
            return false;
        }

        if (IsContactLine(candidate) || IsKnownSectionHeader(candidate))
        {
            return false;
        }

        if (candidate.Contains('|', StringComparison.Ordinal) ||
            candidate.Contains('—', StringComparison.Ordinal) ||
            Regex.IsMatch(candidate, @"\b\d{2}/\d{4}\b|\b\d{4}\b", RegexOptions.CultureInvariant))
        {
            return false;
        }

        if (candidate.Length > 60)
        {
            return false;
        }

        if (strictHeadlineCheck && IsLikelyProfessionalHeadline(candidate))
        {
            return false;
        }

        var words = candidate.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 1 || words.Length > 8)
        {
            return false;
        }

        return words.All(word =>
        {
            var clean = word.Trim('.', ',', '-', '(', ')');
            return clean.Length == 0 ||
                   Regex.IsMatch(clean, @"^[\p{L}\-'\.]+$", RegexOptions.CultureInvariant);
        });
    }

    private static bool IsLikelyProfessionalHeadline(string line)
    {
        var normalized = StripMarkdown(line).ToLowerInvariant();
        var headlineKeywords = new[]
        {
            "técnico", "tecnico", "engenheiro", "encarregado", "analista", "coordenador",
            "gerente", "supervisor", "sistemas", "industrial", "desenvolvedor", "programador",
            "assistant", "manager", "engineer", "technician", "operador", "assistente"
        };
        return headlineKeywords.Any(keyword => normalized.Contains(keyword, StringComparison.Ordinal))
               && normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 3;
    }

    private static int ScorePersonName(string line)
    {
        var candidate = StripMarkdown(line).Trim();
        var words = candidate.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var score = 0;

        if (words.Length is >= 2 and <= 5)
        {
            score += 4;
        }

        if (words.All(w => char.IsUpper(w[0])))
        {
            score += 2;
        }

        if (IsLikelyProfessionalHeadline(candidate))
        {
            score -= 10;
        }

        return score;
    }

    internal static string StripMarkdown(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var cleaned = text.Trim();
        cleaned = Regex.Replace(cleaned, @"^#{1,6}\s*", string.Empty);
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
        var candidate = StripMarkdown(line).Trim(':', ' ', '-', '#');
        if (string.IsNullOrWhiteSpace(candidate) || candidate.Length > 50)
        {
            return false;
        }

        if (LooksLikePersonNameOnly(candidate))
        {
            return false;
        }

        if (IsExperienceHeaderLine(candidate) || IsContactLine(candidate))
        {
            return false;
        }

        var upper = candidate.ToUpperInvariant();
        if (KnownSectionTitles.Any(t => upper.Equals(t, StringComparison.Ordinal)))
        {
            return true;
        }

        if (!ContainsSectionKeyword(candidate))
        {
            return false;
        }

        return IsMostlyUppercase(candidate);
    }

    private static bool IsKnownSectionHeader(string line)
    {
        var candidate = StripMarkdown(line).Trim(':', ' ', '-', '#');
        var upper = candidate.ToUpperInvariant();
        return KnownSectionTitles.Any(t => upper.Equals(t, StringComparison.Ordinal)) ||
               (ContainsSectionKeyword(candidate) && IsMostlyUppercase(candidate));
    }

    private static bool IsMostlyUppercase(string candidate)
    {
        var letters = candidate.Where(char.IsLetter).ToList();
        if (letters.Count == 0)
        {
            return false;
        }

        return letters.Count(char.IsUpper) >= letters.Count * 0.75;
    }

    private static bool LooksLikePersonNameOnly(string line)
    {
        var candidate = StripMarkdown(line).Trim();
        if (string.IsNullOrWhiteSpace(candidate) || candidate.Length > 60)
        {
            return false;
        }

        if (ContainsSectionKeyword(candidate) || IsContactLine(candidate))
        {
            return false;
        }

        if (candidate.Contains('|', StringComparison.Ordinal) ||
            candidate.Contains('—', StringComparison.Ordinal) ||
            Regex.IsMatch(candidate, @"\b\d{2}/\d{4}\b", RegexOptions.CultureInvariant))
        {
            return false;
        }

        var words = candidate.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 2 || words.Length > 8)
        {
            return false;
        }

        return words.All(word =>
        {
            var clean = word.Trim('.', ',', '-', '(', ')');
            return clean.Length == 0 ||
                   Regex.IsMatch(clean, @"^[\p{L}\-'\.]+$", RegexOptions.CultureInvariant);
        });
    }

    private static bool ContainsSectionKeyword(string line)
    {
        var normalized = StripMarkdown(line).Trim(':', ' ', '-').ToUpperInvariant();
        return normalized.Contains("RESUMO") ||
               normalized.Contains("EXPERI") ||
               normalized.Contains("FORMA") ||
               normalized.Contains("HABIL") ||
               normalized.Contains("IDIOMA") ||
               normalized.Contains("OBJETIVO") ||
               normalized.Contains("INFORMA") ||
               normalized.Contains("DADOS") ||
               normalized.Contains("COMPET") ||
               normalized.Contains("CERTIF") ||
               normalized.Contains("SUMMARY") ||
               normalized.Contains("EXPERIENCE") ||
               normalized.Contains("EDUCATION") ||
               normalized.Contains("SKILLS") ||
               normalized.Contains("CONTACT") ||
               normalized.Contains("CONTATO") ||
               normalized.Contains("PROFILE") ||
               normalized.Contains("LANGUAGE") ||
               normalized.Contains("PROJECT");
    }

    private static List<ResumeSectionBlock> BuildSections(List<string> lines, (string Name, string Contact) profile)
    {
        var sections = new List<ResumeSectionBlock>();
        var currentTitle = string.Empty;
        var currentLines = new List<string>();
        var inProfileSection = false;

        foreach (var raw in lines)
        {
            var line = StripMarkdown(raw);
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.Equals(profile.Name, StringComparison.OrdinalIgnoreCase) ||
                line.Equals(profile.Contact, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (IsProfileSectionTitle(line))
            {
                inProfileSection = true;
                FlushSection(sections, currentTitle, currentLines);
                currentTitle = string.Empty;
                currentLines = new List<string>();
                continue;
            }

            if (inProfileSection && (IsLikelyPersonName(line, strictHeadlineCheck: false) || IsContactLine(line)))
            {
                continue;
            }

            if (IsLikelySectionTitle(line))
            {
                inProfileSection = false;
                FlushSection(sections, currentTitle, currentLines);
                currentTitle = line.Trim(':', ' ');
                currentLines = new List<string>();
                continue;
            }

            inProfileSection = false;
            AddLineToSection(currentLines, line);
        }

        FlushSection(sections, currentTitle, currentLines);
        return sections;
    }

    private static void AddLineToSection(List<string> currentLines, string line)
    {
        if (currentLines.Count > 0 && ShouldMergeWithPrevious(currentLines[^1], line))
        {
            currentLines[^1] = $"{currentLines[^1]} {line}";
            return;
        }

        currentLines.Add(line);
    }

    private static bool ShouldMergeWithPrevious(string previous, string line)
    {
        if (IsBulletLine(line) || IsExperienceHeaderLine(line) || IsLikelySectionTitle(line))
        {
            return false;
        }

        if (IsBulletLine(previous) || IsExperienceHeaderLine(previous) || IsLikelySectionTitle(previous))
        {
            return false;
        }

        return line.Length < 50 && !line.EndsWith('.') && !line.EndsWith(';') && previous.Length < 220;
    }

    private static void FlushSection(List<ResumeSectionBlock> sections, string title, List<string> lines)
    {
        if (lines.Count == 0)
        {
            return;
        }

        sections.Add(new ResumeSectionBlock(
            string.IsNullOrWhiteSpace(title) ? "Informações" : title,
            new List<string>(lines)));
    }
}
