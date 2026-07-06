using System.Text.RegularExpressions;

namespace CurriculosProIA.Service.Implementations;

public sealed record ResumeSectionBlock(string Title, List<string> Lines);

public sealed record ResumeLayout(string Name, string Contact, List<ResumeSectionBlock> Sections);

/// <summary>Parser compartilhado entre exportação PDF e Word (PT/EN).</summary>
public static class ResumeLayoutHelper
{
    public static ResumeLayout Parse(string resumeText, string? candidateName = null)
    {
        var lines = NormalizeResumeLines(resumeText);
        var profile = ExtractProfile(lines);
        var name = !string.IsNullOrWhiteSpace(candidateName)
            ? candidateName.Trim()
            : profile.Name;
        var sections = BuildSections(lines, (name, profile.Contact));
        return new ResumeLayout(name, profile.Contact, sections);
    }

    public static bool IsBulletLine(string line)
    {
        var t = line.TrimStart();
        return t.StartsWith("- ") || t.StartsWith("* ") || t.StartsWith("• ");
    }

    public static string StripBulletPrefix(string line) =>
        line.TrimStart('-', '*', '•', ' ').Trim();

    private static List<string> NormalizeResumeLines(string resumeText)
    {
        return (resumeText ?? string.Empty)
            .Replace("\r", string.Empty)
            .Split('\n')
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }

    private static readonly string[] ProfileSectionMarkers =
    [
        "DADOS PESSOAIS",
        "INFORMAÇÕES PESSOAIS",
        "INFORMACOES PESSOAIS",
        "CONTACT",
        "CONTATO"
    ];

    private static readonly string[] HeadlineKeywords =
    [
        "técnico", "tecnico", "engenheiro", "encarregado", "analista", "coordenador",
        "gerente", "supervisor", "sistemas", "industrial", "manutenção", "manutencao",
        "eletro", "hidráulico", "hidraulico", "especialista", "consultor", "desenvolvedor",
        "programador", "assistant", "manager", "engineer", "technician", "maintenance",
        "electrical", "mechanical", "operador", "assistente", "profissional de"
    ];

    private static (string Name, string Contact) ExtractProfile(List<string> lines)
    {
        var cleanedLines = lines.Select(StripMarkdown).ToList();

        var name =
            FindNameAfterProfileSection(cleanedLines) ??
            cleanedLines.FirstOrDefault(IsLikelyPersonName) ??
            cleanedLines
                .Where(IsLikelyPersonName)
                .OrderByDescending(ScorePersonName)
                .FirstOrDefault() ??
            "Currículo Profissional";

        var contact = cleanedLines
            .FirstOrDefault(l =>
                l.Contains("@", StringComparison.OrdinalIgnoreCase) ||
                l.Contains("|", StringComparison.OrdinalIgnoreCase) ||
                Regex.IsMatch(l, @"\(\d{2}\)|\d{8,}", RegexOptions.CultureInvariant)) ?? string.Empty;

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

            for (var j = i + 1; j < Math.Min(i + 4, lines.Count); j++)
            {
                if (IsLikelySectionTitle(lines[j]))
                {
                    break;
                }

                if (IsLikelyPersonName(lines[j]))
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

    private static bool IsLikelyPersonName(string line)
    {
        var candidate = StripMarkdown(line).Trim();
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        if (ContainsSectionKeyword(candidate))
        {
            return false;
        }

        if (IsBulletLine(candidate) || candidate.StartsWith('-'))
        {
            return false;
        }

        if (candidate.Contains('@', StringComparison.Ordinal) ||
            candidate.Contains('|', StringComparison.Ordinal) ||
            candidate.Contains("http", StringComparison.OrdinalIgnoreCase) ||
            Regex.IsMatch(candidate, @"\d{8,}", RegexOptions.CultureInvariant))
        {
            return false;
        }

        if (candidate.Length > 50 || IsLikelyProfessionalHeadline(candidate))
        {
            return false;
        }

        var words = candidate.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 2 || words.Length > 6)
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
        return HeadlineKeywords.Any(keyword => normalized.Contains(keyword, StringComparison.Ordinal));
    }

    private static int ScorePersonName(string line)
    {
        var candidate = StripMarkdown(line).Trim();
        var words = candidate.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var score = 0;

        if (words.Length is >= 2 and <= 4)
        {
            score += 4;
        }

        if (candidate == candidate.ToUpperInvariant())
        {
            score += 3;
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
        {
            return false;
        }

        if (IsLikelyPersonName(candidate))
        {
            return false;
        }

        return ContainsSectionKeyword(candidate);
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
               normalized.Contains("LANGUAGE");
    }

    private static List<ResumeSectionBlock> BuildSections(List<string> lines, (string Name, string Contact) profile)
    {
        var sections = new List<ResumeSectionBlock>();
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
                    sections.Add(new ResumeSectionBlock(currentTitle, currentLines));
                }

                currentTitle = line.Trim(':', ' ');
                currentLines = new List<string>();
                continue;
            }

            currentLines.Add(line);
        }

        if (currentLines.Count > 0)
        {
            sections.Add(new ResumeSectionBlock(currentTitle, currentLines));
        }

        return sections;
    }
}
