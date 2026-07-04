using CurriculosProIA.Domain.Dtos;

namespace CurriculosProIA.Repository.Interfaces;

public interface IPricingConfigRepository
{
    Task<PricingConfigDto?> GetAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(PricingConfigDto config, CancellationToken cancellationToken = default);
}
