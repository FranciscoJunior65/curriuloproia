using CurriculosProIA.Domain.Dtos;
using CurriculosProIA.Repository.Persistence;

namespace CurriculosProIA.Service.Interfaces;

public interface IResumeKeywordService
{
    Task<IReadOnlyList<string>> GenerateKeywordsAsync(
        string resumeText,
        AnalysisInput analysis,
        SiteVagasRow? site,
        CancellationToken cancellationToken = default);
}
