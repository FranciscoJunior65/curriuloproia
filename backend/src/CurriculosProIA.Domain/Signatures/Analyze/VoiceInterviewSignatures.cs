using CurriculosProIA.Domain.Dtos;

namespace CurriculosProIA.Domain.Signatures.Analyze;

public class VoiceInterviewStartSignature
{
    public string? ResumeText { get; set; }
    public AnalysisInput? Analysis { get; set; }
    public string? SiteId { get; set; }
    public string? ResumeId { get; set; }
    public string? AnalysisId { get; set; }
}

public class VoiceInterviewTurnSignature
{
    public string? ResumeText { get; set; }
    public AnalysisInput? Analysis { get; set; }
    public string? SiteId { get; set; }
    public string? AnalysisId { get; set; }
    public string? SimulationId { get; set; }
    public string? CandidateMessage { get; set; }
    public List<VoiceConversationMessageDto>? History { get; set; }
    public int TurnNumber { get; set; }
}

public class VoiceInterviewFinishSignature
{
    public string? SimulationId { get; set; }
    public string? ResumeText { get; set; }
    public AnalysisInput? Analysis { get; set; }
    public string? AnalysisId { get; set; }
    public List<VoiceConversationMessageDto>? History { get; set; }
}
