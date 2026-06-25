using CurriculosProIA.Domain.Dtos;



namespace CurriculosProIA.Domain.Signatures.Analyze;



public class StructuredInterviewStartSignature

{

    public string? ResumeText { get; set; }

    public AnalysisInput? Analysis { get; set; }

    public string? SiteId { get; set; }

    public string? ResumeId { get; set; }

    public string? AnalysisId { get; set; }

}



public class StructuredInterviewBeginVoicePhaseSignature

{

    public string? SimulationId { get; set; }

    public string? AnalysisId { get; set; }

    public string? ResumeText { get; set; }

    public AnalysisInput? Analysis { get; set; }

    public string? SiteId { get; set; }

    public string? CandidateName { get; set; }

    public List<string>? WrittenQuestions { get; set; }

    public List<string>? WrittenAnswers { get; set; }

}



public class StructuredInterviewSubmitPhaseSignature

{

    public string? SimulationId { get; set; }

    public string? AnalysisId { get; set; }

    public int PhaseIndex { get; set; }

    public string? InterviewerScript { get; set; }

    public string? CandidateAnswer { get; set; }

}



public class StructuredInterviewGenerateQuestionsSignature

{

    public string? SimulationId { get; set; }

    public string? AnalysisId { get; set; }

    public string? ResumeText { get; set; }

    public AnalysisInput? Analysis { get; set; }

    public string? SiteId { get; set; }

    public string? Phase1Answer { get; set; }

    public string? CandidateName { get; set; }

}



public class StructuredInterviewFinishSignature

{

    public string? SimulationId { get; set; }

    public string? AnalysisId { get; set; }

    public string? ResumeText { get; set; }

    public AnalysisInput? Analysis { get; set; }

    public string? SiteId { get; set; }

    public string? CandidateName { get; set; }

    public string? IntroScript { get; set; }

    public string? Phase1Answer { get; set; }

    public List<string>? WrittenQuestions { get; set; }

    public List<string>? WrittenAnswers { get; set; }

    public List<string>? WrittenQuestionTypes { get; set; }

}


