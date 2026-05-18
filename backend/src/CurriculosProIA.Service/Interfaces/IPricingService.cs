using CurriculosProIA.Domain.Entities;
using CurriculosProIA.Domain.Dtos;
using CurriculosProIA.Repository.Persistence;
using CurriculosProIA.Domain.Dtos;

namespace CurriculosProIA.Service.Interfaces;

public interface IPricingService
{
    IReadOnlyDictionary<string, PricingPlan> PricingPlans { get; }
    ProfitMarginResult CalculateProfitMargin(string planId);
    PricingPlan? GetPlan(string planId);
}
