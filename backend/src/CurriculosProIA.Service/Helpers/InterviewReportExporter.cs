using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CurriculosProIA.Service.Helpers;

/// <summary>Exporta relatório de entrevista (texto) para PDF e Word.</summary>
public static class InterviewReportExporter
{
    static InterviewReportExporter()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] GeneratePdf(string content)
    {
        var lines = SplitLines(content);

        return QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(42);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Helvetica").LineHeight(1.35f));

                page.Header().Column(col =>
                {
                    col.Item().Text("Simulação de Entrevista — Relatório").Bold().FontSize(14);
                    col.Item().Text(DateTime.Now.ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("pt-BR")))
                        .FontSize(8).FontColor(Colors.Grey.Darken1);
                    col.Item().PaddingBottom(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                page.Content().Column(col =>
                {
                    foreach (var line in lines)
                    {
                        if (IsSeparator(line))
                        {
                            col.Item().PaddingVertical(4);
                            continue;
                        }

                        if (line.StartsWith("PERGUNTA", StringComparison.OrdinalIgnoreCase) ||
                            line.StartsWith("FEEDBACK", StringComparison.OrdinalIgnoreCase) ||
                            line.StartsWith("SIMULAÇÃO", StringComparison.OrdinalIgnoreCase))
                        {
                            col.Item().PaddingTop(6).Text(line).Bold().FontSize(10);
                            continue;
                        }

                        col.Item().Text(line);
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("CurriculosPro IA — ").FontSize(8).FontColor(Colors.Grey.Medium);
                    text.CurrentPageNumber().FontSize(8);
                    text.Span(" / ").FontSize(8);
                    text.TotalPages().FontSize(8);
                });
            });
        }).GeneratePdf();
    }

    public static byte[] GenerateDocx(string content)
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var body = doc.MainDocumentPart!.Document!.Body!;
            body.RemoveAllChildren();

            foreach (var line in SplitLines(content))
            {
                if (IsSeparator(line))
                {
                    body.AppendChild(new Paragraph(new Run(new Text(""))));
                    continue;
                }

                var isHeading = line.StartsWith("PERGUNTA", StringComparison.OrdinalIgnoreCase) ||
                                line.StartsWith("FEEDBACK", StringComparison.OrdinalIgnoreCase) ||
                                line.StartsWith("SIMULAÇÃO", StringComparison.OrdinalIgnoreCase) ||
                                line.StartsWith("====", StringComparison.Ordinal);

                var para = new Paragraph();
                var run = new Run();
                var text = new Text(line) { Space = SpaceProcessingModeValues.Preserve };

                if (isHeading)
                {
                    run.RunProperties = new RunProperties(new Bold());
                }

                run.Append(text);
                para.Append(run);

                if (isHeading)
                {
                    para.ParagraphProperties = new ParagraphProperties(
                        new SpacingBetweenLines { Before = "120", After = "60" });
                }

                body.AppendChild(para);
            }

            doc.MainDocumentPart.Document.Save();
        }

        return stream.ToArray();
    }

    private static string[] SplitLines(string content) =>
        (content ?? string.Empty).Replace("\r\n", "\n").Split('\n');

    private static bool IsSeparator(string line) =>
        line.Trim().All(c => c == '=') && line.Trim().Length >= 8;
}
