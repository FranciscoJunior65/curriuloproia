using CurriculosProIA.Domain.Entities;
using CurriculosProIA.Domain.Dtos;
using CurriculosProIA.Repository.Persistence;
using CurriculosProIA.Domain.Dtos;

namespace CurriculosProIA.Service.Interfaces;

public interface IJobSitesService
{
    Task<List<SiteVagasRow>> GetActiveJobSitesAsync(CancellationToken cancellationToken = default);
    Task<SiteVagasRow?> GetJobSiteByIdAsync(string siteId, CancellationToken cancellationToken = default);
}
