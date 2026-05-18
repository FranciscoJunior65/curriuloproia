using CurriculosProIA.Domain.Dtos;

namespace CurriculosProIA.Domain.Signatures.Analyze;

public class StartInterviewSignature
{
    public string? ResumeText { get; set; }
    public AnalysisInput? Analysis { get; set; }
    public string? SiteId { get; set; }
    public string? ResumeId { get; set; }
}
