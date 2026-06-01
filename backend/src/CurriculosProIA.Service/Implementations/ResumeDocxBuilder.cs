using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace CurriculosProIA.Service.Implementations;

public static class ResumeDocxBuilder
{
    public static byte[] BuildFromText(string resumeText)
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());
            var body = mainPart.Document.Body!;

            foreach (var line in NormalizeLines(resumeText))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var paragraph = new Paragraph();
                var run = new Run();
                var props = new RunProperties();

                if (IsSectionHeader(line))
                {
                    props.Append(new Bold());
                    props.Append(new FontSize { Val = "24" });
                }

                run.Append(props);
                run.Append(new Text(line) { Space = SpaceProcessingModeValues.Preserve });
                paragraph.Append(run);

                if (IsSectionHeader(line))
                {
                    paragraph.ParagraphProperties = new ParagraphProperties(
                        new SpacingBetweenLines { Before = "240", After = "120" });
                }

                body.Append(paragraph);
            }

            mainPart.Document.Save();
        }

        return stream.ToArray();
    }

    private static IEnumerable<string> NormalizeLines(string resumeText)
    {
        return (resumeText ?? string.Empty)
            .Replace("\r", string.Empty)
            .Split('\n')
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s));
    }

    private static bool IsSectionHeader(string line)
    {
        var t = line.Trim().TrimEnd(':');
        if (t.Length > 48)
        {
            return false;
        }

        var upper = t.ToUpperInvariant();
        if (t == upper && t.Length >= 3 && t.Any(char.IsLetter))
        {
            return true;
        }

        return upper is "CONTACT" or "PROFESSIONAL SUMMARY" or "SUMMARY" or "EXPERIENCE" or "WORK EXPERIENCE"
            or "EDUCATION" or "SKILLS" or "LANGUAGES" or "OBJECTIVE" or "PROFILE"
            or "CONTATO" or "RESUMO" or "EXPERIÊNCIA" or "EXPERIENCIA" or "FORMAÇÃO" or "FORMACAO"
            or "HABILIDADES" or "OBJETIVO";
    }
}
