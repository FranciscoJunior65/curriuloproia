using CurriculosProIA.Repository.Persistence;

namespace CurriculosProIA.Repository.Interfaces;

public interface IJobSiteRepository
{
    Task<List<SiteVagasRow>> GetActiveJobSitesAsync(CancellationToken cancellationToken = default);
    Task<SiteVagasRow?> GetJobSiteByIdAsync(string siteId, CancellationToken cancellationToken = default);
}
