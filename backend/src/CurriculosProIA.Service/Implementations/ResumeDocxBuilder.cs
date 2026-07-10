using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace CurriculosProIA.Service.Implementations;

/// <summary>Word com o mesmo layout linear do PDF (uma coluna, ATS-friendly).</summary>
public static class ResumeDocxBuilder
{
    private const string ColorName = "1565C0";
    private const string ColorContact = "616161";
    private const string ColorBody = "424242";
    private const string ColorSectionText = "1976D2";
    private const string ColorBullet = "1E88E5";
    private const string ColorSectionBg = "E3F2FD";
    private const string ColorSectionBorder = "90CAF9";
    private const string FontFamily = "Arial";

    public static byte[] BuildFromText(string resumeText, string? candidateName = null)
    {
        var layout = ResumeLayoutHelper.Parse(resumeText, candidateName);

        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());
            var body = mainPart.Document.Body!;

            AppendName(body, layout.Name);
            if (!string.IsNullOrWhiteSpace(layout.Contact))
            {
                AppendContact(body, layout.Contact);
            }

            AppendHorizontalRule(body);

            foreach (var section in layout.Sections)
            {
                if (!string.IsNullOrWhiteSpace(section.Title))
                {
                    AppendSectionHeader(body, section.Title);
                }

                foreach (var line in section.Lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    if (ResumeLayoutHelper.IsBulletLine(line))
                    {
                        AppendBulletLine(body, ResumeLayoutHelper.StripBulletPrefix(line));
                    }
                    else
                    {
                        AppendBodyLine(body, line);
                    }
                }
            }

            mainPart.Document.Save();
        }

        return stream.ToArray();
    }

    private static void AppendName(Body body, string name)
    {
        var paragraph = new Paragraph(
            new ParagraphProperties(new SpacingBetweenLines { After = "60" }),
            CreateRun(name, 42, bold: true, color: ColorName));
        body.Append(paragraph);
    }

    private static void AppendContact(Body body, string contact)
    {
        var paragraph = new Paragraph(
            new ParagraphProperties(new SpacingBetweenLines { After = "120" }),
            CreateRun(contact, 19, color: ColorContact));
        body.Append(paragraph);
    }

    private static void AppendHorizontalRule(Body body)
    {
        var paragraph = new Paragraph(
            new ParagraphProperties(
                new ParagraphBorders(
                    new BottomBorder
                    {
                        Val = BorderValues.Single,
                        Size = 6,
                        Color = ColorSectionBorder,
                        Space = 1
                    }),
                new SpacingBetweenLines { Before = "120", After = "120" }));
        body.Append(paragraph);
    }

    private static void AppendSectionHeader(Body body, string title)
    {
        var paragraph = new Paragraph(
            new ParagraphProperties(
                new Shading
                {
                    Val = ShadingPatternValues.Clear,
                    Color = "auto",
                    Fill = ColorSectionBg
                },
                new ParagraphBorders(
                    new TopBorder { Val = BorderValues.Single, Size = 4, Color = ColorSectionBorder },
                    new LeftBorder { Val = BorderValues.Single, Size = 4, Color = ColorSectionBorder },
                    new RightBorder { Val = BorderValues.Single, Size = 4, Color = ColorSectionBorder },
                    new BottomBorder { Val = BorderValues.Single, Size = 4, Color = ColorSectionBorder }),
                new Indentation { Left = "80", Right = "80" },
                new SpacingBetweenLines { Before = "140", After = "80" }),
            CreateRun(title.ToUpperInvariant(), 20, bold: true, color: ColorSectionText));

        body.Append(paragraph);
    }

    private static void AppendBodyLine(Body body, string text)
    {
        var paragraph = new Paragraph(
            new ParagraphProperties(
                new SpacingBetweenLines { After = "40", Line = "276", LineRule = LineSpacingRuleValues.Auto }),
            CreateRun(text, 21, color: ColorBody));
        body.Append(paragraph);
    }

    private static void AppendBulletLine(Body body, string text)
    {
        var paragraph = new Paragraph(
            new ParagraphProperties(
                new Indentation { Left = "360", Hanging = "180" },
                new SpacingBetweenLines { After = "40", Line = "276", LineRule = LineSpacingRuleValues.Auto }),
            CreateRun("•", 22, color: ColorBullet),
            CreateRun(" " + SanitizeXmlText(text), 21, color: ColorBody));

        body.Append(paragraph);
    }

    private static Run CreateRun(string text, int halfPoints, bool bold = false, string? color = null)
    {
        var props = new RunProperties(
            new RunFonts { Ascii = FontFamily, HighAnsi = FontFamily },
            new FontSize { Val = halfPoints.ToString() });

        if (bold)
        {
            props.Append(new Bold());
        }

        if (!string.IsNullOrEmpty(color))
        {
            props.Append(new Color { Val = color });
        }

        return new Run(props, new Text(SanitizeXmlText(text)) { Space = SpaceProcessingModeValues.Preserve });
    }

    private static string SanitizeXmlText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (char.IsSurrogate(ch))
            {
                continue;
            }

            if (ch is '\t' or '\n' or '\r' || (ch >= 0x20 && ch <= 0xD7FF) || (ch >= 0xE000 && ch <= 0xFFFD))
            {
                sb.Append(ch);
            }
        }

        return sb.ToString();
    }
}
