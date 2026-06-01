using CurriculosProIA.Domain.Dtos;

namespace CurriculosProIA.Domain.Signatures.Analyze;

public class GenerateEnglishExcelSignature
{
    public string? OriginalText { get; set; }
    public AnalysisInput? Analysis { get; set; }
    public string? SiteId { get; set; }
    public string? Format { get; set; }
    public string? AnalysisId { get; set; }
}
