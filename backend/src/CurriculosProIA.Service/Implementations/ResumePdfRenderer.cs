using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CurriculosProIA.Service.Implementations;

public static class ResumePdfRenderer
{
    static ResumePdfRenderer()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    private const string SidebarBg = "#312e81";
    private const string SidebarTitle = "#a5b4fc";
    private const string SidebarText = "#e2e8f0";
    private const string SidebarMuted = "#c7d2fe";
    private const string Accent = "#6366f1";
    private const string Ink = "#1e293b";
    private const string InkMuted = "#64748b";
    private const string Body = "#475569";
    private const string Line = "#e2e8f0";

    public static byte[] Render(ResumeLayout layout)
    {
        var doc = ResumeDocumentComposer.Compose(layout);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(0);
                page.DefaultTextStyle(x => x.FontSize(9.5f).FontFamily("Helvetica").FontColor(Body));

                page.Content().Row(row =>
                {
                    row.Spacing(0);

                    row.ConstantItem(195).Background(SidebarBg).PaddingVertical(28).PaddingHorizontal(18)
                        .Column(sidebar => ComposeSidebar(sidebar, doc));

                    row.RelativeItem().PaddingVertical(26).PaddingHorizontal(24)
                        .Column(main => ComposeMain(main, doc));
                });
            });
        }).GeneratePdf();
    }

    private static void ComposeSidebar(ColumnDescriptor column, ComposedResumeDocument doc)
    {
        column.Spacing(14);

        column.Item().Text(doc.Name)
            .FontSize(20)
            .SemiBold()
            .FontColor(Colors.White)
            .LineHeight(1.15f);

        if (!string.IsNullOrWhiteSpace(doc.TargetRole))
        {
            column.Item().PaddingTop(4).Text(doc.TargetRole.ToUpperInvariant())
                .FontSize(7.5f)
                .LetterSpacing(0.08f)
                .FontColor(SidebarMuted);
        }

        column.Item().PaddingTop(4).LineHorizontal(0.5f).LineColor("#4f46e5");

        if (doc.ContactItems.Count > 0)
        {
            column.Item().PaddingTop(4).Element(c => SidebarBlock(c, "CONTATO", inner =>
            {
                foreach (var item in doc.ContactItems)
                {
                    inner.Item().PaddingBottom(3).Text(item).FontSize(8.2f).FontColor(SidebarText).LineHeight(1.4f);
                }
            }));
        }

        if (doc.SkillTags.Count > 0)
        {
            column.Item().Element(c => SidebarBlock(c, "HABILIDADES", inner =>
            {
                inner.Item().Column(tagCol =>
                {
                    foreach (var tag in doc.SkillTags)
                    {
                        tagCol.Item().PaddingBottom(3)
                            .Border(0.5f).BorderColor("#4f46e5")
                            .Background("#3730a3")
                            .PaddingVertical(2).PaddingHorizontal(5)
                            .Text(tag).FontSize(7f).FontColor("#e0e7ff");
                    }
                });
            }));
        }

        foreach (var section in doc.SidebarSections.Where(s =>
            !s.Title.Contains("HABILIDADE", StringComparison.OrdinalIgnoreCase) &&
            !s.Title.Contains("SKILL", StringComparison.OrdinalIgnoreCase) &&
            !s.Title.Contains("COMPET", StringComparison.OrdinalIgnoreCase)))
        {
            column.Item().Element(c => SidebarBlock(c, section.Title.ToUpperInvariant(), inner =>
            {
                foreach (var line in section.Lines)
                {
                    inner.Item().PaddingBottom(2).Text(FormatSidebarLine(line))
                        .FontSize(8.2f).FontColor(SidebarText).LineHeight(1.45f);
                }
            }));
        }
    }

    private static void ComposeMain(ColumnDescriptor column, ComposedResumeDocument doc)
    {
        column.Spacing(16);

        foreach (var section in doc.MainSections)
        {
            if (IsExperienceSectionTitle(section.Title) && doc.Jobs.Count > 0)
            {
                continue;
            }

            column.Item().Element(c => MainSection(c, section.Title, inner =>
            {
                foreach (var line in section.Lines)
                {
                    inner.Item().PaddingBottom(3).Text(line).FontSize(9.5f).LineHeight(1.55f).FontColor(Body);
                }
            }));
        }

        if (doc.Jobs.Count > 0)
        {
            column.Item().Element(c => MainSection(c, "EXPERIÊNCIA", inner =>
            {
                foreach (var job in doc.Jobs)
                {
                    inner.Item().PaddingBottom(10).Element(jobCol =>
                    {
                        jobCol.Column(jobInner =>
                        {
                            jobInner.Item().Row(head =>
                            {
                                head.RelativeItem().Column(left =>
                                {
                                    if (!string.IsNullOrWhiteSpace(job.Title))
                                    {
                                        left.Item().Text(job.Title).SemiBold().FontSize(10.5f).FontColor(Ink);
                                    }

                                    if (!string.IsNullOrWhiteSpace(job.Company))
                                    {
                                        left.Item().Text(job.Company).FontSize(9f).SemiBold().FontColor(Accent);
                                    }
                                });

                                if (!string.IsNullOrWhiteSpace(job.Period))
                                {
                                    head.AutoItem().Background("#f1f5f9").PaddingVertical(2).PaddingHorizontal(6)
                                        .Text(job.Period).FontSize(7.5f).SemiBold().FontColor(InkMuted);
                                }
                            });

                            foreach (var bullet in job.Bullets)
                            {
                                jobInner.Item().PaddingTop(2).Row(bulletRow =>
                                {
                                    bulletRow.ConstantItem(12).Text("•").FontSize(10).FontColor(Accent);
                                    bulletRow.RelativeItem().Text(bullet).FontSize(9f).LineHeight(1.5f).FontColor(Body);
                                });
                            }

                            jobInner.Item().PaddingTop(6).LineHorizontal(0.5f).LineColor(Line);
                        });
                    });
                }
            }));
        }
    }

    private static void SidebarBlock(IContainer container, string title, Action<ColumnDescriptor> content)
    {
        container.Column(col =>
        {
            col.Item().PaddingBottom(6).Text(title)
                .FontSize(7f)
                .Bold()
                .LetterSpacing(0.12f)
                .FontColor(SidebarTitle);
            content(col);
        });
    }

    private static void MainSection(IContainer container, string title, Action<ColumnDescriptor> content)
    {
        container.Column(col =>
        {
            col.Item().PaddingBottom(8).Row(header =>
            {
                header.AutoItem().Text(title.ToUpperInvariant())
                    .FontSize(7.5f)
                    .Bold()
                    .LetterSpacing(0.12f)
                    .FontColor(Accent);
                header.RelativeItem().PaddingLeft(8).AlignMiddle()
                    .LineHorizontal(0.5f).LineColor("#c7d2fe");
            });
            content(col);
        });
    }

    private static string FormatSidebarLine(string line)
    {
        if (ResumeLayoutHelper.IsBulletLine(line))
        {
            return ResumeLayoutHelper.StripBulletPrefix(line);
        }

        return line;
    }

    private static bool IsExperienceSectionTitle(string title)
    {
        var upper = title.ToUpperInvariant();
        return upper.Contains("EXPERI", StringComparison.Ordinal) ||
               upper.Contains("EXPERIENCE", StringComparison.Ordinal);
    }
}
