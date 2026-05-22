namespace CurriculosProIA.Domain.Dtos;

public class InterviewPersonaDto
{
    public string Name { get; set; } = "";
    public string Role { get; set; } = "";
    public string Company { get; set; } = "";
    public string Initials { get; set; } = "";
    public string AvatarColor { get; set; } = "#6366f1";
}

public class VoiceConversationMessageDto
{
    public string Role { get; set; } = ""; // interviewer | candidate
    public string Content { get; set; } = "";
}

public class VoiceInterviewStartResult
{
    public string? SimulationId { get; set; }
    public InterviewPersonaDto Persona { get; set; } = new();
    public string OpeningMessage { get; set; } = "";
}

public class VoiceInterviewTurnResult
{
    public string InterviewerMessage { get; set; } = "";
    public bool ShouldEnd { get; set; }
    public string Phase { get; set; } = "opening";
    public int TurnNumber { get; set; }
}

public class VoiceInterviewSummaryResult
{
    public int Score { get; set; }
    public string OverallFeedback { get; set; } = "";
    public List<string> Strengths { get; set; } = [];
    public List<string> Improvements { get; set; } = [];
    public List<string> Highlights { get; set; } = [];
}
