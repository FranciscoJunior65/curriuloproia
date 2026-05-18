using CurriculosProIA.Domain.Dtos;

namespace CurriculosProIA.Domain.Signatures.Analyze;

public class EvaluateInterviewSignature
{
    public string? Question { get; set; }
    public string? Answer { get; set; }
    public string? ResumeText { get; set; }
    public AnalysisInput? Analysis { get; set; }
    public string? SimulationId { get; set; }
}
