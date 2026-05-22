using CurriculosProIA.Domain.Dtos;

namespace CurriculosProIA.Domain.Signatures.Analyze;

public class SearchJobsSignature
{
    public AnalysisInput? Analysis { get; set; }
    public string? SiteId { get; set; }
    public string? Location { get; set; }
    public string? ResumeText { get; set; }
    public string? ResumeId { get; set; }
    public string? AnalysisId { get; set; }
}
