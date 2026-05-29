using System.Text.RegularExpressions;

namespace CurriculosProIA.Service.Helpers;

public static class JobContactExtractor
{
    private static readonly Regex EmailRegex = new(
        @"[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PhoneRegex = new(
        @"(?:\+55\s?)?(?:\(?\d{2}\)?\s?)?\d{4,5}[\s\-]?\d{4}",
        RegexOptions.Compiled);

    public static List<string> ExtractFromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new List<string>();
        }

        var hints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match m in EmailRegex.Matches(text))
        {
            hints.Add($"E-mail: {m.Value}");
        }

        foreach (Match m in PhoneRegex.Matches(text))
        {
            var digits = new string(m.Value.Where(char.IsDigit).ToArray());
            if (digits.Length >= 10)
            {
                hints.Add($"Telefone: {m.Value.Trim()}");
            }
        }

        return hints.Take(5).ToList();
    }
}
