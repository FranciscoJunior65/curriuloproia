using CurriculosProIA.Domain.Dtos;

namespace CurriculosProIA.Service.Interfaces;

public interface IJobBoardScraperService
{
    Task<List<JobListing>> SearchIndeedAsync(
        IReadOnlyList<string> searchTerms,
        string location = "Brasil",
        int maxResults = 10,
        CancellationToken cancellationToken = default);

    Task<List<JobListing>> SearchCathoAsync(
        IReadOnlyList<string> searchTerms,
        string location = "Brasil",
        int maxResults = 10,
        CancellationToken cancellationToken = default);

    Task EnrichJobDetailsAsync(
        JobListing job,
        CancellationToken cancellationToken = default);
}
