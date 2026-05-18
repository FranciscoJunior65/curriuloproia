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
    Task SaveFoundJobsAsync(string userId, string resumeId, string siteId, List<JobListing> jobs, CancellationToken cancellationToken = default);
}
