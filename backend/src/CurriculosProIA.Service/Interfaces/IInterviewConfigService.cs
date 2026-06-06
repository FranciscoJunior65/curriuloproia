using CurriculosProIA.Domain.Dtos;

namespace CurriculosProIA.Service.Interfaces;

public interface IInterviewConfigService
{
    Task<InterviewConfigDto> GetConfigAsync(CancellationToken cancellationToken = default);
    Task<InterviewConfigDto> SaveConfigAsync(InterviewConfigDto config, CancellationToken cancellationToken = default);
    void ClearCache();
}
