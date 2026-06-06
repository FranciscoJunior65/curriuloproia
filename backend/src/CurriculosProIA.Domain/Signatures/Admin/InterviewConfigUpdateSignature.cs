namespace CurriculosProIA.Domain.Signatures.Admin;

public class InterviewConfigUpdateSignature
{
    public string? IntroductionPrompt { get; set; }
    public string? QuestionsPrompt { get; set; }
    public string? FeedbackPrompt { get; set; }
    public int? Phase1Minutes { get; set; }
    public int? Phase2Minutes { get; set; }
    public int? Phase3Minutes { get; set; }
    public int? MaxVideoSpeechSeconds { get; set; }
    public int? MaxSegmentSeconds { get; set; }
}
