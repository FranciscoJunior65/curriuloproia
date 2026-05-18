using CurriculosProIA.Domain.Entities;
using CurriculosProIA.Domain.Dtos;
using CurriculosProIA.Repository.Persistence;
using CurriculosProIA.Domain.Dtos;

namespace CurriculosProIA.Service.Interfaces;

public interface IInterviewSimulationService
{
    Task<(string? SimulationId, List<string> Questions)> StartInterviewAsync(
        string resumeText,
        AnalysisInput analysis,
        string? siteId,
        string? userId,
        string? resumeId,
        CancellationToken cancellationToken = default);

    Task<InterviewEvaluation> EvaluateAnswerAsync(
        string question,
        string answer,
        string resumeText,
        AnalysisInput analysis,
        CancellationToken cancellationToken = default);

    Task<bool> SaveInterviewMessageAsync(
        string simulationId,
        string question,
        string answer,
        InterviewEvaluation evaluation,
        int order,
        CancellationToken cancellationToken = default);

    Task<int> FinishInterviewAsync(
        string simulationId,
        List<InterviewAnswerItem> allAnswers,
        CancellationToken cancellationToken = default);

    Task<InterviewDetailDto?> GetInterviewByIdAsync(string simulationId, CancellationToken cancellationToken = default);

    Task<List<SimulacaoEntrevistaRow>> GetUserInterviewsAsync(string userId, CancellationToken cancellationToken = default);

    string BuildInterviewDownloadContent(InterviewDetailDto interview);
}
