using CurriculosProIA.Domain.Dtos;
using CurriculosProIA.Domain.Entities;

namespace CurriculosProIA.Service.Interfaces;

public interface IPricingService
{
    IReadOnlyDictionary<string, PricingPlan> PricingPlans { get; }
    Task<PricingConfigDto> GetPricingConfigAsync(CancellationToken cancellationToken = default);
    Task<PricingConfigDto> SavePricingConfigAsync(PricingConfigDto config, CancellationToken cancellationToken = default);
    void ClearCache();
    Task<IReadOnlyDictionary<string, PricingPlan>> GetPricingPlansAsync(CancellationToken cancellationToken = default);
    Task<PricingPlan?> GetPlanAsync(string planId, CancellationToken cancellationToken = default);
    PricingPlan? GetPlan(string planId);
    ProfitMarginResult CalculateProfitMargin(string planId);
}
