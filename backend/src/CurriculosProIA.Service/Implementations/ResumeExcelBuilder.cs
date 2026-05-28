using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace CurriculosProIA.Service.Implementations;

internal static class ResumeExcelBuilder
{
    public static byte[] BuildFromText(string resumeText, string sheetName = "Resume EN")
    {
        using var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook, true))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();
            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var sheetData = new SheetData();

            uint rowIndex = 1;
            foreach (var rawLine in resumeText.Split('\n'))
            {
                var line = rawLine.TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(line))
                {
                    rowIndex++;
                    continue;
                }

                var row = new Row { RowIndex = rowIndex };
                row.Append(CreateTextCell(line, rowIndex));
                sheetData.Append(row);
                rowIndex++;
            }

            worksheetPart.Worksheet = new Worksheet(sheetData);
            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            sheets.Append(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = SanitizeSheetName(sheetName)
            });
        }

        return stream.ToArray();
    }

    private static Cell CreateTextCell(string text, uint rowIndex, string column = "A")
    {
        return new Cell
        {
            CellReference = column + rowIndex,
            DataType = CellValues.InlineString,
            InlineString = new InlineString { Text = new Text(text) }
        };
    }

    private static string SanitizeSheetName(string name)
    {
        var invalid = new[] { '\\', '/', '*', '?', ':', '[', ']' };
        var sanitized = name;
        foreach (var ch in invalid)
        {
            sanitized = sanitized.Replace(ch, ' ');
        }

        return sanitized.Length > 31 ? sanitized[..31] : sanitized;
    }
}
