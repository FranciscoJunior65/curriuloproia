using System.Text.RegularExpressions;

namespace CurriculosProIA.Service.Implementations;

public sealed record ResumeSectionBlock(string Title, List<string> Lines);

public sealed record ResumeLayout(string Name, string Contact, List<ResumeSectionBlock> Sections);

/// <summary>Parser compartilhado entre exportação PDF e Word (PT/EN).</summary>
public static class ResumeLayoutHelper
{
    public static ResumeLayout Parse(string resumeText)
    {
        var lines = NormalizeResumeLines(resumeText);
        var profile = ExtractProfile(lines);
        var sections = BuildSections(lines, profile);
        return new ResumeLayout(profile.Name, profile.Contact, sections);
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

        var normalized = candidate.ToUpperInvariant();
        return candidate == normalized ||
               normalized.Contains("RESUMO") ||
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
