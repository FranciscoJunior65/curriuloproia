using CurriculosProIA.Domain.Dtos;



namespace CurriculosProIA.Service.Interfaces;



public interface IStructuredInterviewService

{

    Task<StructuredInterviewStatusResult> GetStatusAsync(

        string analysisId,

        string userId,

        string? resumeId,

        CancellationToken cancellationToken = default);



    Task<StructuredInterviewStartResult> StartAsync(

        string resumeText,

        AnalysisInput analysis,

        string? siteId,

        string? userId,

        string? resumeId,

        CancellationToken cancellationToken = default);



    Task<StructuredInterviewVoicePhaseResult> BeginVoicePhaseAsync(

        string resumeText,

        AnalysisInput analysis,

        string? siteId,

        string candidateName,

        CancellationToken cancellationToken = default);



    Task SavePhaseAsync(

        string simulationId,

        int phaseIndex,

        string interviewerScript,

        string candidateAnswer,

        CancellationToken cancellationToken = default);



    Task SaveWrittenAnswersAsync(

        string simulationId,

        IReadOnlyList<string> questions,

        IReadOnlyList<string> answers,

        CancellationToken cancellationToken = default);



    Task<StructuredInterviewFinishResult> FinishAsync(

        string? simulationId,

        string resumeText,

        AnalysisInput analysis,

        string? siteId,

        string candidateName,

        string introScript,

        IReadOnlyList<string> writtenQuestions,

        IReadOnlyList<string> writtenAnswers,

        string phase1Answer,

        IReadOnlyList<string>? writtenQuestionTypes = null,

        CancellationToken cancellationToken = default);

}


