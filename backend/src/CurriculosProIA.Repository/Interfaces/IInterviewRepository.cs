using CurriculosProIA.Domain.Dtos;
using CurriculosProIA.Repository.Persistence;

namespace CurriculosProIA.Repository.Interfaces;

public interface IInterviewRepository
{
    Task<string?> CreateInterviewSimulationAsync(string userId, string resumeId, string siteId, List<string> questions, string areaFoco, CancellationToken cancellationToken = default);
    Task<bool> SaveInterviewMessageAsync(string simulationId, string question, string answer, InterviewEvaluation evaluation, int order, CancellationToken cancellationToken = default);
    Task<FinishInterviewResult> UpdateSimulationAnswersAsync(string simulationId, List<InterviewAnswerItem> allAnswers, CancellationToken cancellationToken = default);
    Task<InterviewDetailDto?> GetInterviewByIdAsync(string simulationId, CancellationToken cancellationToken = default);
    Task<List<SimulacaoEntrevistaRow>> GetUserInterviewsAsync(string userId, int limit = 50, CancellationToken cancellationToken = default);
    Task<SimulacaoEntrevistaRow?> GetLatestInterviewForResumeAsync(string userId, string resumeId, CancellationToken cancellationToken = default);
    Task SaveStructuredPhaseAsync(string simulationId, int phaseIndex, string interviewerScript, string candidateAnswer, CancellationToken cancellationToken = default);
    Task SaveStructuredWrittenAnswersAsync(string simulationId, IReadOnlyList<string> questions, IReadOnlyList<string> answers, CancellationToken cancellationToken = default);
    Task SaveStructuredFeedbackAsync(string simulationId, string feedbackScript, InterviewEvaluation evaluation, CancellationToken cancellationToken = default);
    Task UpdateInterviewQuestionsAsync(string simulationId, List<string> questions, CancellationToken cancellationToken = default);
    Task SaveFoundJobsAsync(string userId, string resumeId, string siteId, List<JobListing> jobs, CancellationToken cancellationToken = default);
}
