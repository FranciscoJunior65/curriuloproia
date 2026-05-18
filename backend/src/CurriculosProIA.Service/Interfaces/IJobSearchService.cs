using CurriculosProIA.Domain.Entities;
using CurriculosProIA.Domain.Dtos;
using CurriculosProIA.Repository.Persistence;
using CurriculosProIA.Domain.Dtos;

namespace CurriculosProIA.Service.Interfaces;

public interface IJobSearchService
{
    Task<JobSearchResult> SearchJobsBySiteAsync(
        string siteId,
        AnalysisInput analysis,
        string location = "Brasil",
        string? resumeText = null,
        string? userId = null,
        string? resumeId = null,
        CancellationToken cancellationToken = default);
}
