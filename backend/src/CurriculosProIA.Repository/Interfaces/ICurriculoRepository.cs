using CurriculosProIA.Repository.Persistence;

namespace CurriculosProIA.Repository.Interfaces;

public interface ICurriculoRepository
{
    Task<string?> SaveImportedResumeAsync(string userId, string siteId, string fileName, string fileType, string textContent, string? creditId = null, object? analysisData = null, CancellationToken cancellationToken = default);
    Task<CurriculoImportadoRow?> GetResumeByIdAsync(string resumeId, CancellationToken cancellationToken = default);
}
