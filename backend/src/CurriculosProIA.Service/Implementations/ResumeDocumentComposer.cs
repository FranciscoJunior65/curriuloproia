using System.Text.RegularExpressions;

namespace CurriculosProIA.Service.Implementations;

public sealed record ComposedJob(
    string Title,
    string Company,
    string Period,
    IReadOnlyList<string> Bullets);

public sealed record ComposedResumeDocument(
    string Name,
    string? TargetRole,
    IReadOnlyList<string> ContactItems,
    IReadOnlyList<string> SkillTags,
    IReadOnlyList<ResumeSectionBlock> SidebarSections,
    IReadOnlyList<ResumeSectionBlock> MainSections,
    IReadOnlyList<ComposedJob> Jobs);

public static class ResumeDocumentComposer
{
    private static readonly string[] SidebarSectionKeys =
    [
        "HABILIDADE", "SKILL", "COMPET", "FORMAÇÃO", "FORMACAO", "EDUCATION",
        "CERTIFICA", "IDIOMA", "LANGUAGE", "CURSO"
    ];

    private static readonly string[] MainSectionKeys =
    [
        "RESUMO", "SUMMARY", "OBJETIVO", "EXPERI", "EXPERIENCE", "PROJETO", "PROJECT"
    ];

    public static ComposedResumeDocument Compose(ResumeLayout layout)
    {
        var sidebar = new List<ResumeSectionBlock>();
        var main = new List<ResumeSectionBlock>();
        var jobs = new List<ComposedJob>();

        foreach (var section in layout.Sections.Where(s => s.Lines.Count > 0))
        {
            if (IsSidebarSection(section.Title))
            {
                sidebar.Add(section);
                continue;
            }

            if (IsExperienceSection(section.Title))
            {
                var parsed = ParseJobs(section.Lines);
                if (parsed.Count > 0)
                {
                    jobs.AddRange(parsed);
                }
                else
                {
                    main.Add(section);
                }

                continue;
            }

            if (IsMainSection(section.Title) || string.IsNullOrWhiteSpace(section.Title))
            {
                main.Add(section);
                continue;
            }

            main.Add(section);
        }

        return new ComposedResumeDocument(
            layout.Name,
            InferTargetRole(layout, jobs),
            ParseContactItems(layout.Contact),
            ExtractSkillTags(sidebar, layout.Sections),
            sidebar,
            main,
            jobs);
    }

    private static bool IsSidebarSection(string title)
    {
        var upper = title.ToUpperInvariant();
        return SidebarSectionKeys.Any(key => upper.Contains(key, StringComparison.Ordinal));
    }

    private static bool IsMainSection(string title)
    {
        var upper = title.ToUpperInvariant();
        return MainSectionKeys.Any(key => upper.Contains(key, StringComparison.Ordinal));
    }

    private static bool IsExperienceSection(string title)
    {
        var upper = title.ToUpperInvariant();
        return upper.Contains("EXPERI", StringComparison.Ordinal) ||
               upper.Contains("EXPERIENCE", StringComparison.Ordinal);
    }

    private static List<string> ParseContactItems(string contact)
    {
        if (string.IsNullOrWhiteSpace(contact))
        {
            return [];
        }

        return contact
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(part => part.Length > 0)
            .ToList();
    }

    private static List<string> ExtractSkillTags(
        IReadOnlyList<ResumeSectionBlock> sidebarSections,
        IReadOnlyList<ResumeSectionBlock> allSections)
    {
        var skillsSection = sidebarSections.FirstOrDefault(s =>
            s.Title.Contains("HABILIDADE", StringComparison.OrdinalIgnoreCase) ||
            s.Title.Contains("SKILL", StringComparison.OrdinalIgnoreCase) ||
            s.Title.Contains("COMPET", StringComparison.OrdinalIgnoreCase));

        skillsSection ??= allSections.FirstOrDefault(s =>
            s.Title.Contains("HABILIDADE", StringComparison.OrdinalIgnoreCase) ||
            s.Title.Contains("SKILL", StringComparison.OrdinalIgnoreCase));

        if (skillsSection == null)
        {
            return [];
        }

        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in skillsSection.Lines)
        {
            if (ResumeLayoutHelper.IsBulletLine(line))
            {
                var bullet = ResumeLayoutHelper.StripBulletPrefix(line);
                foreach (var part in bullet.Split([':', ',', ';'], StringSplitOptions.RemoveEmptyEntries))
                {
                    var tag = part.Trim();
                    if (tag.Length is >= 2 and <= 36)
                    {
                        tags.Add(tag);
                    }
                }
            }
            else
            {
                foreach (var part in line.Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries))
                {
                    var tag = part.Trim();
                    if (tag.Length is >= 2 and <= 36 && !tag.Contains(':'))
                    {
                        tags.Add(tag);
                    }
                }
            }
        }

        return tags.Take(24).ToList();
    }

    private static List<ComposedJob> ParseJobs(IReadOnlyList<string> lines)
    {
        var jobs = new List<ComposedJob>();
        ComposedJob? current = null;

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (ResumeLayoutHelper.IsExperienceHeaderLine(line))
            {
                if (current != null)
                {
                    jobs.Add(current);
                }

                current = ParseJobHeader(line);
                continue;
            }

            if (ResumeLayoutHelper.IsBulletLine(line))
            {
                current ??= new ComposedJob(string.Empty, string.Empty, string.Empty, []);
                var bullets = current.Bullets.ToList();
                bullets.Add(ResumeLayoutHelper.StripBulletPrefix(line));
                current = current with { Bullets = bullets };
                continue;
            }

            if (current != null && current.Bullets.Count == 0 && string.IsNullOrWhiteSpace(current.Title))
            {
                current = ParseJobHeader(line);
            }
        }

        if (current != null)
        {
            jobs.Add(current);
        }

        return jobs;
    }

    private static ComposedJob ParseJobHeader(string line)
    {
        var parts = line.Split('|', StringSplitOptions.TrimEntries);
        if (parts.Length >= 3)
        {
            return new ComposedJob(parts[1], parts[0], parts[2], []);
        }

        if (parts.Length == 2)
        {
            return new ComposedJob(parts[1], parts[0], string.Empty, []);
        }

        return new ComposedJob(line, string.Empty, string.Empty, []);
    }

    private static string? InferTargetRole(ResumeLayout layout, IReadOnlyList<ComposedJob> jobs)
    {
        var summary = layout.Sections.FirstOrDefault(s =>
            s.Title.Contains("RESUMO", StringComparison.OrdinalIgnoreCase) ||
            s.Title.Contains("SUMMARY", StringComparison.OrdinalIgnoreCase));

        if (summary?.Lines.FirstOrDefault() is { } summaryLine)
        {
            var match = Regex.Match(
                summaryLine,
                @"\b((?:Desenvolvedor|Analista|Engenheiro|Programador|Arquiteto|Consultor|Designer|Gerente|Coordenador|Tech Lead|DevOps|Administrador)[^,.]{0,60})",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }
        }

        var firstJob = jobs.FirstOrDefault(j => !string.IsNullOrWhiteSpace(j.Title));
        return firstJob?.Title;
    }
}
