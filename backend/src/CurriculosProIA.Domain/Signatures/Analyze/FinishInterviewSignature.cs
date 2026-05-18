using CurriculosProIA.Domain.Dtos;

namespace CurriculosProIA.Domain.Signatures.Analyze;

public class FinishInterviewSignature
{
    public string? SimulationId { get; set; }
    public List<InterviewAnswerItem>? AllAnswers { get; set; }
}
