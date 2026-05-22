using CurriculosProIA.Domain.Dtos;

namespace CurriculosProIA.Repository.Interfaces;

public interface IAnalysisRepository
{
    Task<List<AnaliseCurriculoListItemDto>> GetUserAnalysesAsync(string userId, int limit = 50, int offset = 0, CancellationToken cancellationToken = default);
    Task<AnaliseCurriculoListItemDto?> GetAnalysisByIdAsync(string analysisId, CancellationToken cancellationToken = default);
    Task<string?> SaveAnalysisAsync(
        string resumeId,
        string userId,
        string siteId,
        ResumeAnalysisResult analysis,
        CancellationToken cancellationToken = default);
    Task<bool> UserOwnsAnalysisAsync(string userId, string analysisId, CancellationToken cancellationToken = default);
    Task<bool> MarkServiceUsedAsync(string analysisId, string serviceKey, CancellationToken cancellationToken = default);
    Task<string?> GetAnalysisIdByResumeIdAsync(string userId, string resumeId, CancellationToken cancellationToken = default);
    Task<AnalysisServicesStatusDto> GetServicesStatusAsync(string analysisId, CancellationToken cancellationToken = default);
    Task<bool> HasInterviewForResumeAsync(string resumeId, CancellationToken cancellationToken = default);
    Task<PendingServicesSummaryDto> GetPendingServicesSummaryAsync(string userId, CancellationToken cancellationToken = default);
}
