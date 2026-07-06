using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace CurriculosProIA.Service.Implementations;

/// <summary>Word com layout sidebar + conteúdo principal (alinhado ao PDF).</summary>
public static class ResumeDocxBuilder
{
    private const string SidebarBg = "312E81";
    private const string SidebarTitle = "A5B4FC";
    private const string SidebarText = "E2E8F0";
    private const string SidebarMuted = "C7D2FE";
    private const string Accent = "6366F1";
    private const string Ink = "1E293B";
    private const string InkMuted = "64748B";
    private const string Body = "475569";
    private const string FontFamily = "Arial";

    public static byte[] BuildFromText(string resumeText, string? candidateName = null)
    {
        var layout = ResumeLayoutHelper.Parse(resumeText, candidateName);
        var doc = ResumeDocumentComposer.Compose(layout);

        using var stream = new MemoryStream();
        using (var wordDoc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = wordDoc.AddMainDocumentPart();
            EnsureWordprocessingDefaults(mainPart);

            mainPart.Document ??= new Document();
            var body = mainPart.Document.Body ?? mainPart.Document.AppendChild(new Body());
            body.RemoveAllChildren();

            var table = new Table();
            table.AppendChild(new TableProperties(
                new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
                new TableLayout { Type = TableLayoutValues.Fixed },
                new TableBorders(
                    new TopBorder { Val = BorderValues.None },
                    new LeftBorder { Val = BorderValues.None },
                    new RightBorder { Val = BorderValues.None },
                    new BottomBorder { Val = BorderValues.None },
                    new InsideHorizontalBorder { Val = BorderValues.None },
                    new InsideVerticalBorder { Val = BorderValues.None })));

            var grid = new TableGrid();
            grid.Append(new GridColumn { Width = "3200" });
            grid.Append(new GridColumn { Width = "6800" });
            table.AppendChild(grid);

            var row = new TableRow(new TableRowProperties(new CantSplit()));
            row.Append(CreateSidebarCell(doc));
            row.Append(CreateMainCell(doc));
            table.Append(row);
            body.Append(table);

            body.Append(new SectionProperties(
                new PageSize { Width = 11906U, Height = 16838U },
                new PageMargin
                {
                    Top = 360,
                    Right = 360,
                    Bottom = 360,
                    Left = 360,
                    Header = 0U,
                    Footer = 0U,
                    Gutter = 0U
                }));

            mainPart.Document.Save();
            wordDoc.Save();
        }

        stream.Position = 0;
        return stream.ToArray();
    }

    private static void EnsureWordprocessingDefaults(MainDocumentPart mainPart)
    {
        if (mainPart.StyleDefinitionsPart == null)
        {
            var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
            stylesPart.Styles = CreateDefaultStyles();
        }

        if (mainPart.DocumentSettingsPart == null)
        {
            var settingsPart = mainPart.AddNewPart<DocumentSettingsPart>();
            settingsPart.Settings = new Settings(
                new Compatibility(
                    new CompatibilitySetting
                    {
                        Name = new EnumValue<CompatSettingNameValues>(CompatSettingNameValues.CompatibilityMode),
                        Uri = "http://schemas.microsoft.com/office/word",
                        Val = "15"
                    }));
        }

        if (mainPart.FontTablePart == null)
        {
            var fontPart = mainPart.AddNewPart<FontTablePart>();
            fontPart.Fonts = CreateFontTable();
        }
    }

    private static Styles CreateDefaultStyles()
    {
        var styles = new Styles();

        styles.Append(new DocDefaults(
            new RunPropertiesDefault(
                new RunPropertiesBaseStyle(
                    new RunFonts { Ascii = FontFamily, HighAnsi = FontFamily },
                    new FontSize { Val = "22" },
                    new Languages { Val = "pt-BR", EastAsia = "pt-BR", Bidi = "ar-SA" })),
            new ParagraphPropertiesDefault(
                new ParagraphPropertiesBaseStyle(
                    new SpacingBetweenLines { After = "0", Line = "276", LineRule = LineSpacingRuleValues.Auto }))));

        styles.Append(new Style(
            new StyleName { Val = "Normal" },
            new PrimaryStyle(),
            new StyleRunProperties(
                new RunFonts { Ascii = FontFamily, HighAnsi = FontFamily },
                new FontSize { Val = "22" }))
        {
            Type = StyleValues.Paragraph,
            StyleId = "Normal",
            Default = true
        });

        return styles;
    }

    private static Fonts CreateFontTable() =>
        new(
            new Font(
                new Name { Val = FontFamily },
                new Panose1Number { Val = "020B0604030504040204" },
                new FontCharSet { Val = "00" },
                new FontFamily { Val = FontFamilyValues.Swiss },
                new Pitch { Val = FontPitchValues.Variable }),
            new Font(
                new Name { Val = "Calibri" },
                new Panose1Number { Val = "020F0502020204030204" },
                new FontCharSet { Val = "00" },
                new FontFamily { Val = FontFamilyValues.Swiss },
                new Pitch { Val = FontPitchValues.Variable }));

    private static TableCell CreateSidebarCell(ComposedResumeDocument doc)
    {
        var cell = new TableCell();
        cell.AppendChild(new TableCellProperties(
            new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = "3200" },
            new Shading { Val = ShadingPatternValues.Clear, Fill = SidebarBg, Color = "auto" },
            new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Top },
            new TableCellMargin(
                new TopMargin { Width = "280", Type = TableWidthUnitValues.Dxa },
                new BottomMargin { Width = "280", Type = TableWidthUnitValues.Dxa },
                new LeftMargin { Width = "220", Type = TableWidthUnitValues.Dxa },
                new RightMargin { Width = "180", Type = TableWidthUnitValues.Dxa })));

        AppendSidebarParagraph(cell, doc.Name, 36, bold: true, color: "FFFFFF", after: "80");
        if (!string.IsNullOrWhiteSpace(doc.TargetRole))
        {
            AppendSidebarParagraph(cell, doc.TargetRole, 14, color: SidebarMuted, after: "120");
        }

        AppendSidebarRule(cell);

        if (doc.ContactItems.Count > 0)
        {
            AppendSidebarHeading(cell, "CONTATO");
            foreach (var item in doc.ContactItems)
            {
                AppendSidebarParagraph(cell, item, 16, color: SidebarText, after: "40");
            }
        }

        if (doc.SkillTags.Count > 0)
        {
            AppendSidebarHeading(cell, "HABILIDADES");
            AppendSidebarParagraph(cell, string.Join("  ·  ", doc.SkillTags), 15, color: SidebarText, after: "80");
        }

        foreach (var section in doc.SidebarSections.Where(s =>
            !s.Title.Contains("HABILIDADE", StringComparison.OrdinalIgnoreCase) &&
            !s.Title.Contains("SKILL", StringComparison.OrdinalIgnoreCase) &&
            !s.Title.Contains("COMPET", StringComparison.OrdinalIgnoreCase)))
        {
            AppendSidebarHeading(cell, section.Title.ToUpperInvariant());
            foreach (var line in section.Lines)
            {
                var text = ResumeLayoutHelper.IsBulletLine(line)
                    ? ResumeLayoutHelper.StripBulletPrefix(line)
                    : line;
                AppendSidebarParagraph(cell, text, 16, color: SidebarText, after: "40");
            }
        }

        return cell;
    }

    private static TableCell CreateMainCell(ComposedResumeDocument doc)
    {
        var cell = new TableCell();
        cell.AppendChild(new TableCellProperties(
            new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = "6800" },
            new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Top },
            new TableCellMargin(
                new TopMargin { Width = "260", Type = TableWidthUnitValues.Dxa },
                new BottomMargin { Width = "260", Type = TableWidthUnitValues.Dxa },
                new LeftMargin { Width = "280", Type = TableWidthUnitValues.Dxa },
                new RightMargin { Width = "240", Type = TableWidthUnitValues.Dxa })));

        foreach (var section in doc.MainSections)
        {
            if (IsExperienceSectionTitle(section.Title) && doc.Jobs.Count > 0)
            {
                continue;
            }

            AppendMainHeading(cell, section.Title);
            foreach (var line in section.Lines)
            {
                AppendMainBody(cell, line, after: "50");
            }
        }

        if (doc.Jobs.Count > 0)
        {
            AppendMainHeading(cell, "EXPERIÊNCIA");
            foreach (var job in doc.Jobs)
            {
                AppendJobHeader(cell, job);
                foreach (var bullet in job.Bullets)
                {
                    AppendMainBullet(cell, bullet);
                }

                AppendMainSpacer(cell, "60");
            }
        }

        return cell;
    }

    private static void AppendSidebarRule(TableCell cell)
    {
        cell.Append(new Paragraph(
            new ParagraphProperties(
                new ParagraphBorders(new BottomBorder
                {
                    Val = BorderValues.Single,
                    Size = 4,
                    Color = "4F46E5",
                    Space = 1
                }),
                new SpacingBetweenLines { Before = "60", After = "120" })));
    }

    private static void AppendSidebarHeading(TableCell cell, string text)
    {
        AppendSidebarParagraph(cell, text, 14, bold: true, color: SidebarTitle, after: "60", caps: true);
    }

    private static void AppendSidebarParagraph(
        TableCell cell,
        string text,
        int halfPoints,
        bool bold = false,
        string color = SidebarText,
        string after = "40",
        bool caps = false)
    {
        var props = new ParagraphProperties(new SpacingBetweenLines { After = after });
        var run = CreateRun(text, halfPoints, bold, color);
        if (caps)
        {
            run.RunProperties ??= new RunProperties();
            run.RunProperties.Append(new Caps());
        }

        cell.Append(new Paragraph(props, run));
    }

    private static void AppendMainHeading(TableCell cell, string title)
    {
        cell.Append(new Paragraph(
            new ParagraphProperties(new SpacingBetweenLines { Before = "80", After = "60" }),
            CreateRun(title.ToUpperInvariant(), 15, bold: true, color: Accent)));
    }

    private static void AppendMainBody(TableCell cell, string text, string after = "40")
    {
        cell.Append(new Paragraph(
            new ParagraphProperties(new SpacingBetweenLines { After = after, Line = "276", LineRule = LineSpacingRuleValues.Auto }),
            CreateRun(text, 19, color: Body)));
    }

    private static void AppendJobHeader(TableCell cell, ComposedJob job)
    {
        var title = new List<string>();
        if (!string.IsNullOrWhiteSpace(job.Title))
        {
            title.Add(job.Title);
        }

        if (!string.IsNullOrWhiteSpace(job.Company))
        {
            title.Add(job.Company);
        }

        var header = string.Join(" — ", title);
        if (!string.IsNullOrWhiteSpace(job.Period))
        {
            header = string.IsNullOrWhiteSpace(header) ? job.Period : $"{header}    {job.Period}";
        }

        cell.Append(new Paragraph(
            new ParagraphProperties(new SpacingBetweenLines { Before = "80", After = "30", Line = "276", LineRule = LineSpacingRuleValues.Auto }),
            CreateRun(header, 20, bold: true, color: Ink)));
    }

    private static void AppendMainBullet(TableCell cell, string text)
    {
        cell.Append(new Paragraph(
            new ParagraphProperties(
                new Indentation { Left = "320", Hanging = "200" },
                new SpacingBetweenLines { After = "30", Line = "276", LineRule = LineSpacingRuleValues.Auto }),
            CreateRun("•", 20, color: Accent),
            CreateRun(" " + SanitizeXmlText(text), 19, color: Body)));
    }

    private static void AppendMainSpacer(TableCell cell, string after)
    {
        cell.Append(new Paragraph(new ParagraphProperties(new SpacingBetweenLines { After = after })));
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

    private static bool IsExperienceSectionTitle(string title)
    {
        var upper = title.ToUpperInvariant();
        return upper.Contains("EXPERI", StringComparison.Ordinal) ||
               upper.Contains("EXPERIENCE", StringComparison.Ordinal);
    }
}
