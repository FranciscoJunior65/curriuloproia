namespace CurriculosProIA.Domain.Dtos;



public class StructuredInterviewStatusResult

{

    public bool AlreadyCompleted { get; set; }

    public string? SimulationId { get; set; }

    public bool CanDownload { get; set; }

    public StructuredInterviewSavedFeedback? SavedFeedback { get; set; }

}



public class StructuredInterviewSavedFeedback

{

    public int Score { get; set; }

    public string OverallFeedback { get; set; } = "";

    public string FeedbackScript { get; set; } = "";

    public List<string> Strengths { get; set; } = [];

    public List<string> Improvements { get; set; } = [];

}



public class WrittenQuestionDto

{

    public string Text { get; set; } = "";

    /// <summary>open = texto livre; choice = múltipla escolha</summary>

    public string Type { get; set; } = "open";

    public List<string> Options { get; set; } = [];

}



public class StructuredInterviewStartResult

{

    public string? SimulationId { get; set; }

    public InterviewPersonaDto Persona { get; set; } = new();

    public string CandidateName { get; set; } = "";

    public List<WrittenQuestionDto> WrittenQuestions { get; set; } = [];

    public int Phase1Minutes { get; set; }

}



public class StructuredInterviewVoicePhaseResult

{

    public string IntroScript { get; set; } = "";

}



public class StructuredInterviewQuestionsResult

{

    public List<string> Questions { get; set; } = [];

}



public class StructuredInterviewFinishResult

{

    public int Score { get; set; }

    public string FeedbackScript { get; set; } = "";

    public string OverallFeedback { get; set; } = "";

    public List<string> Strengths { get; set; } = [];

    public List<string> Improvements { get; set; } = [];

    public string? SimulationId { get; set; }

}


