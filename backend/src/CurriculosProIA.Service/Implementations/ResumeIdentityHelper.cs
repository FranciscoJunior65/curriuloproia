using System.Text;
using System.Text.RegularExpressions;

namespace CurriculosProIA.Service.Implementations;

/// <summary>Dados verificados do candidato extraídos do currículo original (fonte da verdade).</summary>
public sealed record ResumeIdentity(
    string Name,
    string? ContactLine,
    IReadOnlyList<string> Skills,
    IReadOnlyList<string> Companies)
{
    public static ResumeIdentity Empty { get; } = new(string.Empty, null, [], []);
}

public static class ResumeIdentityHelper
{
    private static readonly string[] ProfileMarkers =
    [
        "DADOS PESSOAIS", "INFORMAÇÕES PESSOAIS", "INFORMACOES PESSOAIS", "CONTACT", "CONTATO"
    ];

    private static readonly string[] SkillsSectionMarkers =
    [
        "HABILIDADES", "COMPETÊNCIAS", "COMPETENCIAS", "SKILLS", "TECHNICAL SKILLS"
    ];

    private static readonly string[] ExperienceSectionMarkers =
    [
        "EXPERIÊNCIA", "EXPERIENCIA", "EXPERIENCE", "WORK EXPERIENCE"
    ];

    private static readonly Regex PlaceholderPattern = new(
        @"não especificado|nao especificado|not specified|nível não|nivel nao|\(não informado\)|\(nao informado\)|@email\.com\b|exemplo@|candidato@",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static ResumeIdentity Extract(string? resumeText)
    {
        if (string.IsNullOrWhiteSpace(resumeText))
        {
            return ResumeIdentity.Empty;
        }

        var lines = NormalizeLines(resumeText);
        var layout = ResumeLayoutHelper.Parse(resumeText);
        var name = layout.Name;
        if (string.IsNullOrWhiteSpace(name) ||
            name.Equals("Currículo Profissional", StringComparison.OrdinalIgnoreCase))
        {
            name = ExtractNameFromTopLines(lines) ?? string.Empty;
        }

        var contact = !string.IsNullOrWhiteSpace(layout.Contact)
            ? layout.Contact.Trim()
            : lines.FirstOrDefault(ResumeLayoutHelper.IsContactLinePublic);

        var skills = ExtractSkills(lines);
        var companies = ExtractCompanies(lines);

        return new ResumeIdentity(
            name.Trim(),
            string.IsNullOrWhiteSpace(contact) ? null : contact.Trim(),
            skills,
            companies);
    }

    public static string EnforceFidelity(string generatedText, ResumeIdentity identity, string? originalText = null)
    {
        if (string.IsNullOrWhiteSpace(generatedText) || string.IsNullOrWhiteSpace(identity.Name))
        {
            return generatedText;
        }

        var originalHasLanguages = !string.IsNullOrWhiteSpace(originalText) &&
            Regex.IsMatch(originalText, @"\bIDIOMAS\b|\bLANGUAGES\b", RegexOptions.IgnoreCase);

        var lines = generatedText
            .Replace("\r", string.Empty)
            .Split('\n')
            .Select(l => l.TrimEnd())
            .ToList();

        var output = new List<string>();
        var inProfile = false;
        var profileNameWritten = false;
        var profileContactWritten = false;
        var skipUntilNextSection = false;
        var currentSection = string.Empty;

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var trimmed = line.Trim();

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                if (!skipUntilNextSection)
                {
                    if (output.Count > 0 && string.IsNullOrWhiteSpace(output[^1]))
                    {
                        continue;
                    }

                    output.Add(string.Empty);
                }

                continue;
            }

            if (IsSectionTitle(trimmed))
            {
                skipUntilNextSection = ShouldSkipSection(trimmed, originalHasLanguages);
                if (!skipUntilNextSection)
                {
                    currentSection = trimmed;
                    inProfile = IsProfileSection(trimmed);
                    profileNameWritten = false;
                    profileContactWritten = false;
                    output.Add(trimmed);
                }
                else
                {
                    inProfile = false;
                }

                continue;
            }

            if (skipUntilNextSection)
            {
                continue;
            }

            if (inProfile)
            {
                if (!profileNameWritten)
                {
                    output.Add(identity.Name);
                    profileNameWritten = true;
                    continue;
                }

                if (!profileContactWritten && identity.ContactLine != null)
                {
                    output.Add(identity.ContactLine);
                    profileContactWritten = true;
                    continue;
                }

                if (!profileContactWritten && ResumeLayoutHelper.IsContactLinePublic(trimmed))
                {
                    if (identity.ContactLine != null)
                    {
                        output.Add(identity.ContactLine);
                    }
                    else if (!LooksLikeFabricatedContact(trimmed))
                    {
                        output.Add(trimmed);
                    }

                    profileContactWritten = true;
                    continue;
                }

                if (profileContactWritten || profileNameWritten)
                {
                    continue;
                }
            }

            if (PlaceholderPattern.IsMatch(trimmed))
            {
                continue;
            }

            if (ContainsForeignName(trimmed, identity.Name))
            {
                continue;
            }

            output.Add(trimmed);
        }

        EnsureProfileHeader(output, identity);
        return CollapseBlankLines(string.Join('\n', output));
    }

    public static string BuildIdentityPromptBlock(ResumeIdentity identity, bool portuguese = true)
    {
        if (string.IsNullOrWhiteSpace(identity.Name))
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        if (portuguese)
        {
            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine("IDENTIDADE VERIFICADA DO CANDIDATO (FONTE DA VERDADE — NÃO ALTERAR):");
            sb.AppendLine($"- Nome completo (usar EXATAMENTE na 1ª linha de DADOS PESSOAIS): {identity.Name}");
            if (!string.IsNullOrWhiteSpace(identity.ContactLine))
            {
                sb.AppendLine($"- Contato (usar EXATAMENTE na 2ª linha de DADOS PESSOAIS): {identity.ContactLine}");
            }
            else
            {
                sb.AppendLine("- Contato: use SOMENTE telefone/e-mail/LinkedIn/GitHub que aparecem no currículo original. NÃO invente e-mail.");
            }

            if (identity.Companies.Count > 0)
            {
                sb.AppendLine($"- Empresas do original (não inventar outras): {string.Join(", ", identity.Companies)}");
            }

            if (identity.Skills.Count > 0)
            {
                sb.AppendLine($"- Tecnologias/habilidades do original: {string.Join(", ", identity.Skills.Take(40))}");
            }

            sb.AppendLine("PROIBIDO: trocar o nome, inventar e-mail, idioma, certificação, empresa ou tecnologia.");
            sb.AppendLine("═══════════════════════════════════════════════════════════════");
        }
        else
        {
            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine("VERIFIED CANDIDATE IDENTITY (SOURCE OF TRUTH — DO NOT CHANGE):");
            sb.AppendLine($"- Full name (use EXACTLY as first line under CONTACT): {identity.Name}");
            if (!string.IsNullOrWhiteSpace(identity.ContactLine))
            {
                sb.AppendLine($"- Contact (use EXACTLY as second line under CONTACT): {identity.ContactLine}");
            }
            else
            {
                sb.AppendLine("- Contact: use ONLY phone/email/LinkedIn/GitHub from the source resume. Do NOT invent email.");
            }

            if (identity.Companies.Count > 0)
            {
                sb.AppendLine($"- Employers from source (do not invent): {string.Join(", ", identity.Companies)}");
            }

            if (identity.Skills.Count > 0)
            {
                sb.AppendLine($"- Skills/technologies from source: {string.Join(", ", identity.Skills.Take(40))}");
            }

            sb.AppendLine("FORBIDDEN: change name, invent email, language, certification, employer or technology.");
            sb.AppendLine("═══════════════════════════════════════════════════════════════");
        }

        return sb.ToString();
    }

    private static void EnsureProfileHeader(List<string> output, ResumeIdentity identity)
    {
        var hasProfile = output.Any(IsProfileSection);
        if (hasProfile)
        {
            return;
        }

        var insertAt = 0;
        output.Insert(insertAt++, "DADOS PESSOAIS");
        output.Insert(insertAt++, identity.Name);
        if (!string.IsNullOrWhiteSpace(identity.ContactLine))
        {
            output.Insert(insertAt, identity.ContactLine);
        }
    }

    private static bool ShouldSkipSection(string title, bool originalHasLanguages)
    {
        var upper = title.ToUpperInvariant();
        if (!upper.Contains("IDIOMA", StringComparison.Ordinal) &&
            !upper.Contains("LANGUAGE", StringComparison.Ordinal))
        {
            return false;
        }

        return !originalHasLanguages;
    }

    private static bool ContainsForeignName(string line, string expectedName)
    {
        if (string.IsNullOrWhiteSpace(expectedName))
        {
            return false;
        }

        var expectedParts = expectedName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (expectedParts.Length == 0)
        {
            return false;
        }

        if (line.Contains(expectedName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!ResumeLayoutHelper.IsLikelyPersonNamePublic(line))
        {
            return false;
        }

        var lineParts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (lineParts.Length < 2 || lineParts.Length > 6)
        {
            return false;
        }

        return true;
    }

    private static bool LooksLikeFabricatedContact(string line)
    {
        return line.Contains("@email.com", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("exemplo@", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("candidato@", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractNameFromTopLines(IReadOnlyList<string> lines)
    {
        foreach (var line in lines.Take(8))
        {
            if (IsSectionTitle(line) || ResumeLayoutHelper.IsContactLinePublic(line))
            {
                continue;
            }

            if (ResumeLayoutHelper.IsLikelyPersonNamePublic(line))
            {
                return line.Trim();
            }
        }

        return null;
    }

    private static List<string> ExtractSkills(IReadOnlyList<string> lines)
    {
        var skills = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var inSkills = false;

        foreach (var line in lines)
        {
            if (IsSectionTitle(line))
            {
                inSkills = SkillsSectionMarkers.Any(m =>
                    line.Contains(m, StringComparison.OrdinalIgnoreCase));
                continue;
            }

            if (!inSkills)
            {
                continue;
            }

            if (ResumeLayoutHelper.IsBulletLine(line))
            {
                var bullet = ResumeLayoutHelper.StripBulletPrefix(line);
                foreach (var part in bullet.Split([':', ',', ';'], StringSplitOptions.RemoveEmptyEntries))
                {
                    var skill = part.Trim();
                    if (skill.Length >= 2 && skill.Length <= 40)
                    {
                        skills.Add(skill);
                    }
                }
            }
            else if (line.Length >= 2 && line.Length <= 40 && !line.Contains('|'))
            {
                skills.Add(line.Trim());
            }
        }

        return skills.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<string> ExtractCompanies(IReadOnlyList<string> lines)
    {
        var companies = new List<string>();
        var inExperience = false;

        foreach (var line in lines)
        {
            if (IsSectionTitle(line))
            {
                inExperience = ExperienceSectionMarkers.Any(m =>
                    line.Contains(m, StringComparison.OrdinalIgnoreCase));
                continue;
            }

            if (!inExperience)
            {
                continue;
            }

            if (ResumeLayoutHelper.IsExperienceHeaderLine(line))
            {
                var company = line.Split('|')[0].Trim();
                if (!string.IsNullOrWhiteSpace(company) && company.Length <= 80)
                {
                    companies.Add(company);
                }
            }
            else if (!ResumeLayoutHelper.IsBulletLine(line) &&
                     !ResumeLayoutHelper.IsExperienceHeaderLine(line))
            {
                var company = ExtractCompanyFromExperienceLine(line);
                if (!string.IsNullOrWhiteSpace(company))
                {
                    companies.Add(company);
                }
            }
        }

        return companies
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList();
    }

    private static string? ExtractCompanyFromExperienceLine(string line)
    {
        var normalized = Regex.Replace(line.Trim(), @"([a-záàâãéêíóôõúç])([A-Z])", "$1 $2");
        normalized = Regex.Replace(normalized, @"\s{2,}", " ").Trim();

        if (normalized.Length < 3 || normalized.Length > 100)
        {
            return null;
        }

        if (Regex.IsMatch(normalized, @"^(Desenvolvedor|Analista|Programador|Engenheiro|Developer|Engineer)\b", RegexOptions.IgnoreCase))
        {
            return null;
        }

        var cityCut = Regex.Match(normalized, @"^(.+?)\s+(?:Sorocaba|Belo Horizonte|São Paulo|Rio de Janeiro|Curitiba|Porto Alegre|Recife|Salvador|Brasília)[,\s]", RegexOptions.IgnoreCase);
        if (cityCut.Success)
        {
            return cityCut.Groups[1].Value.Trim();
        }

        if (normalized.Contains('|', StringComparison.Ordinal))
        {
            return normalized.Split('|')[0].Trim();
        }

        if (Regex.IsMatch(normalized, @"\b(19|20)\d{2}\b"))
        {
            return null;
        }

        return normalized.Length <= 60 ? normalized : normalized[..60].Trim();
    }

    private static List<string> NormalizeLines(string text) =>
        text.Replace("\r", string.Empty)
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();

    private static bool IsProfileSection(string line)
    {
        var upper = line.ToUpperInvariant();
        return ProfileMarkers.Any(m => upper.Contains(m, StringComparison.Ordinal));
    }

    private static bool IsSectionTitle(string line)
    {
        var normalized = line.Trim(':', ' ', '-').ToUpperInvariant();
        return ResumeLayoutHelper.IsKnownSectionHeaderPublic(normalized) ||
               ProfileMarkers.Any(m => normalized.Contains(m, StringComparison.Ordinal)) ||
               SkillsSectionMarkers.Any(m => normalized.Contains(m, StringComparison.Ordinal)) ||
               ExperienceSectionMarkers.Any(m => normalized.Contains(m, StringComparison.Ordinal));
    }

    private static string CollapseBlankLines(string text)
    {
        var lines = text.Split('\n').ToList();
        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0]))
        {
            lines.RemoveAt(0);
        }

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return string.Join('\n', lines);
    }
}
