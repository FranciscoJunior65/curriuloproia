using CurriculosProIA.Domain.Dtos;

namespace CurriculosProIA.Repository.Interfaces;

public interface IAnalysisRepository
{
    Task<List<AnaliseCurriculoListItemDto>> GetUserAnalysesAsync(string userId, int limit = 50, int offset = 0, CancellationToken cancellationToken = default);
    Task<AnaliseCurriculoListItemDto?> GetAnalysisByIdAsync(string analysisId, CancellationToken cancellationToken = default);
}
