using CurriculosProIA.Domain.Dtos;

namespace CurriculosProIA.Service.Interfaces;

public interface IGoogleJobsSearchService
{
    /// <summary>
    /// Busca vagas agregadas pelo Google (LinkedIn, Indeed, Catho, Gupy, etc.) via SerpAPI ou fallback com link.
    /// </summary>
    Task<JobSearchResult> SearchAsync(
        IReadOnlyList<string> searchTerms,
        string location = "Brasil",
        int maxResults = 20,
        CancellationToken cancellationToken = default);
}
