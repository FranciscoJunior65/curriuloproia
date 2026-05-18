using CurriculosProIA.Domain.Entities;
using CurriculosProIA.Domain.Dtos;
using CurriculosProIA.Repository.Persistence;
using CurriculosProIA.Domain.Dtos;

using CurriculosProIA.Service.Interfaces;
using CurriculosProIA.Repository.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CurriculosProIA.Service.Implementations;

public class PricingService : IPricingService
{
    private static readonly IReadOnlyDictionary<string, PricingPlan> Plans = BuildPlans();

    public IReadOnlyDictionary<string, PricingPlan> PricingPlans => Plans;

    public PricingPlan? GetPlan(string planId) =>
        Plans.TryGetValue(planId, out var plan) ? plan : null;

    public ProfitMarginResult CalculateProfitMargin(string planId)
    {
        if (!Plans.TryGetValue(planId, out var plan))
        {
            throw new ArgumentException("Plano não encontrado", nameof(planId));
        }

        const decimal estimatedCostPerAnalysis = 0.50m;
        var totalCost = estimatedCostPerAnalysis * plan.Analyses;
        var profit = plan.PriceBRL - totalCost;
        var margin = plan.PriceBRL > 0 ? (profit / plan.PriceBRL) * 100 : 0;

        return new ProfitMarginResult
        {
            TotalCost = Math.Round(totalCost, 2),
            Profit = Math.Round(profit, 2),
            Margin = Math.Round(margin, 1)
        };
    }

    private static IReadOnlyDictionary<string, PricingPlan> BuildPlans() =>
        new Dictionary<string, PricingPlan>
        {
            ["single"] = new PricingPlan
            {
                Id = "single",
                Name = "Análise Única",
                Description = "1 análise completa otimizada para sites de vagas",
                Analyses = 1,
                PriceBRL = 7.90m,
                PriceUSD = 1.98m,
                Features =
                [
                    "1 análise completa com IA",
                    "Otimização para sites de vagas (Gupy, LinkedIn, Vagas.com, InfoJobs, Catho, Indeed)",
                    "Simulador de entrevista com IA",
                    "Currículo melhorado em PDF ou WORD",
                    "Palavras-chave estratégicas",
                    "Análise única para um site específico"
                ]
            },
            ["pack3"] = new PricingPlan
            {
                Id = "pack3",
                Name = "Pacote 3 Análises",
                Description = "3 análises completas otimizadas para diferentes sites",
                Analyses = 3,
                PriceBRL = 27.90m,
                PriceUSD = 5.58m,
                Savings = "Melhor custo-benefício",
                Features =
                [
                    "3 análises completas com IA",
                    "Otimização para sites de vagas (Gupy, LinkedIn, Vagas.com, InfoJobs, Catho, Indeed)",
                    "Simulador de entrevista com IA",
                    "Currículo melhorado em PDF ou WORD",
                    "Palavras-chave estratégicas"
                ]
            },
            ["pack5"] = new PricingPlan
            {
                Id = "pack5",
                Name = "Pacote 5 Análises",
                Description = "5 análises completas otimizadas para diferentes sites",
                Analyses = 5,
                PriceBRL = 37.90m,
                PriceUSD = 7.58m,
                Savings = "Economize R$ 1,60",
                Features =
                [
                    "5 análises completas com IA",
                    "Otimização para sites de vagas (Gupy, LinkedIn, Vagas.com, InfoJobs, Catho, Indeed)",
                    "Simulador de entrevista com IA",
                    "Currículo melhorado em PDF ou WORD",
                    "Palavras-chave estratégicas"
                ]
            },
            ["english"] = new PricingPlan
            {
                Id = "english",
                Name = "Currículo em Inglês",
                Description = "Geração de currículo profissional em inglês (apenas PDF e WORD, sem análise)",
                Analyses = 0,
                PriceBRL = 17.90m,
                PriceBRLBundle = 5.90m,
                PriceUSD = 1.98m,
                Features =
                [
                    "Currículo traduzido e adaptado para padrões internacionais",
                    "Formatação ATS-friendly",
                    "Download em PDF ou WORD",
                    "Otimizado para vagas globais",
                    "Adaptação cultural profissional"
                ]
            }
        };
}
