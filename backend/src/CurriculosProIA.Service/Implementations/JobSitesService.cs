using CurriculosProIA.Domain.Entities;
using CurriculosProIA.Domain.Dtos;
using CurriculosProIA.Repository.Persistence;
using CurriculosProIA.Domain.Dtos;

using CurriculosProIA.Service.Interfaces;
using CurriculosProIA.Repository.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CurriculosProIA.Service.Implementations;

public class JobSitesService : IJobSitesService
{
    private readonly IJobSiteRepository _jobSites;

    public JobSitesService(IJobSiteRepository jobSites)
    {
        _jobSites = jobSites;
    }

    public Task<List<SiteVagasRow>> GetActiveJobSitesAsync(CancellationToken cancellationToken = default) =>
        _jobSites.GetActiveJobSitesAsync(cancellationToken);

    public Task<SiteVagasRow?> GetJobSiteByIdAsync(string siteId, CancellationToken cancellationToken = default) =>
        _jobSites.GetJobSiteByIdAsync(siteId, cancellationToken);
}
