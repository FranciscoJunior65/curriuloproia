using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CurriculosProIA.Service.Implementations;

/// <summary>Layout linear ATS-friendly — uma coluna, sem sidebar.</summary>
public static class ResumePdfRenderer
{
    static ResumePdfRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] Render(ResumeLayout layout)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(10.5f).FontFamily("Helvetica").FontColor(Colors.Grey.Darken3));

                page.Content().Column(column =>
                {
                    column.Spacing(5);
                    column.Item().Text(layout.Name).FontSize(21).SemiBold().FontColor(Colors.Blue.Darken3);

                    if (!string.IsNullOrWhiteSpace(layout.Contact))
                    {
                        column.Item().Text(layout.Contact).FontSize(9.5f).FontColor(Colors.Grey.Darken1);
                    }

                    column.Item().PaddingTop(8).PaddingBottom(2).LineHorizontal(1).LineColor(Colors.Blue.Lighten3);

                    foreach (var section in layout.Sections)
                    {
                        if (!string.IsNullOrWhiteSpace(section.Title))
                        {
                            column.Item().PaddingTop(7).Element(x =>
                            {
                                x.Background(Colors.Blue.Lighten5)
                                    .Border(1)
                                    .BorderColor(Colors.Blue.Lighten3)
                                    .PaddingVertical(4)
                                    .PaddingHorizontal(8)
                                    .Text(section.Title.ToUpperInvariant())
                                    .FontSize(10)
                                    .SemiBold()
                                    .FontColor(Colors.Blue.Darken2);
                            });
                        }

                        foreach (var line in section.Lines)
                        {
                            if (string.IsNullOrWhiteSpace(line))
                            {
                                continue;
                            }

                            if (ResumeLayoutHelper.IsBulletLine(line))
                            {
                                var bulletText = ResumeLayoutHelper.StripBulletPrefix(line);
                                column.Item().PaddingBottom(2).Row(row =>
                                {
                                    row.Spacing(6);
                                    row.ConstantItem(8).Text("•").FontSize(11).FontColor(Colors.Blue.Medium);
                                    row.RelativeItem().Text(bulletText).LineHeight(1.35f);
                                });
                            }
                            else
                            {
                                column.Item().PaddingBottom(2).Text(line).LineHeight(1.35f);
                            }
                        }
                    }
                });
            });
        }).GeneratePdf();
    }
}
