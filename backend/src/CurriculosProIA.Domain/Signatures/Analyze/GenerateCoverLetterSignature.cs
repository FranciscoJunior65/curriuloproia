using CurriculosProIA.Domain.Dtos;

namespace CurriculosProIA.Domain.Signatures.Analyze;

public class GenerateCoverLetterSignature
{
    public string? ResumeText { get; set; }
    public AnalysisInput? Analysis { get; set; }
    public string? SiteId { get; set; }
    public string? AnalysisId { get; set; }
}
