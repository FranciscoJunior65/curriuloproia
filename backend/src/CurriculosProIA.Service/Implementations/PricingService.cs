using CurriculosProIA.Domain.Dtos;
using CurriculosProIA.Domain.Entities;
using CurriculosProIA.Repository.Interfaces;
using CurriculosProIA.Service.Interfaces;

namespace CurriculosProIA.Service.Implementations;

public class PricingService : IPricingService
{
    private readonly IPricingConfigRepository _pricingConfig;
    private PricingConfigDto? _cachedConfig;

    public PricingService(IPricingConfigRepository pricingConfig)
    {
        _pricingConfig = pricingConfig;
    }

    public async Task<PricingConfigDto> GetPricingConfigAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedConfig != null)
        {
            return _cachedConfig;
        }

        var fromDb = await _pricingConfig.GetAsync(cancellationToken);
        _cachedConfig = NormalizeConfig(fromDb ?? CreateDefaultConfig());
        return _cachedConfig;
    }

    public async Task<PricingConfigDto> SavePricingConfigAsync(
        PricingConfigDto config,
        CancellationToken cancellationToken = default)
    {
        ValidateConfig(config);
        var normalized = NormalizeConfig(config);
        await _pricingConfig.SaveAsync(normalized, cancellationToken);
        _cachedConfig = normalized;
        return normalized;
    }

    public void ClearCache() => _cachedConfig = null;

    public async Task<IReadOnlyDictionary<string, PricingPlan>> GetPricingPlansAsync(
        CancellationToken cancellationToken = default)
    {
        var config = await GetPricingConfigAsync(cancellationToken);
        return BuildPlans(config);
    }

    public async Task<PricingPlan?> GetPlanAsync(string planId, CancellationToken cancellationToken = default)
    {
        var plans = await GetPricingPlansAsync(cancellationToken);
        return plans.TryGetValue(planId, out var plan) ? plan : null;
    }

    public PricingPlan? GetPlan(string planId)
    {
        var plans = BuildPlans(_cachedConfig ?? CreateDefaultConfig());
        return plans.TryGetValue(planId, out var plan) ? plan : null;
    }

    public IReadOnlyDictionary<string, PricingPlan> PricingPlans =>
        BuildPlans(_cachedConfig ?? CreateDefaultConfig());

    public ProfitMarginResult CalculateProfitMargin(string planId)
    {
        if (!PricingPlans.TryGetValue(planId, out var plan))
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

    private static PricingConfigDto CreateDefaultConfig() => new()
    {
        CreditUnitPriceBRL = 7.90m,
        SingleDiscountPercent = 0,
        Pack3DiscountPercent = 0,
        Pack5DiscountPercent = 4.05m,
        Pack3PriceOverride = 27.90m,
        EnglishPriceBRL = 17.90m,
        EnglishBundlePriceBRL = 5.90m
    };

    private static PricingConfigDto NormalizeConfig(PricingConfigDto config)
    {
        var normalized = new PricingConfigDto
        {
            CreditUnitPriceBRL = Math.Round(config.CreditUnitPriceBRL, 2),
            SingleDiscountPercent = ClampPercent(config.SingleDiscountPercent),
            Pack3DiscountPercent = ClampPercent(config.Pack3DiscountPercent),
            Pack5DiscountPercent = ClampPercent(config.Pack5DiscountPercent),
            EnglishPriceBRL = Math.Round(config.EnglishPriceBRL, 2),
            EnglishBundlePriceBRL = Math.Round(config.EnglishBundlePriceBRL, 2),
            TransactionFeeBRL = Math.Round(Math.Max(0, config.TransactionFeeBRL), 2),
            SinglePriceOverride = Math.Round(config.SinglePriceBRL, 2),
            Pack3PriceOverride = Math.Round(config.Pack3PriceBRL, 2),
            Pack5PriceOverride = Math.Round(config.Pack5PriceBRL, 2)
        };

        return normalized;
    }

    private static void ValidateConfig(PricingConfigDto config)
    {
        if (config.CreditUnitPriceBRL <= 0)
        {
            throw new ArgumentException("Valor do crédito deve ser maior que zero.");
        }

        if (config.EnglishPriceBRL <= 0 || config.EnglishBundlePriceBRL <= 0)
        {
            throw new ArgumentException("Preços do pacote inglês devem ser maiores que zero.");
        }

        if (config.TransactionFeeBRL < 0)
        {
            throw new ArgumentException("Taxa de transação não pode ser negativa.");
        }
    }

    private static decimal ClampPercent(decimal value) =>
        Math.Max(0, Math.Min(100, value));

    private static string? BuildSavingsLabel(int analyses, decimal unitPrice, decimal finalPrice)
    {
        if (analyses <= 1)
        {
            return null;
        }

        var full = unitPrice * analyses;
        var savings = full - finalPrice;
        if (savings <= 0.01m)
        {
            return analyses == 3 ? "Melhor custo-benefício" : null;
        }

        return $"Economize R$ {savings:N2}".Replace('.', ',');
    }

    private static IReadOnlyDictionary<string, PricingPlan> BuildPlans(PricingConfigDto config)
    {
        var singlePrice = config.SinglePriceBRL;
        var pack3Price = config.Pack3PriceBRL;
        var pack5Price = config.Pack5PriceBRL;

        return new Dictionary<string, PricingPlan>
        {
            ["single"] = new PricingPlan
            {
                Id = "single",
                Name = "Análise Única",
                Description = "1 análise completa otimizada para sites de vagas",
                Analyses = 1,
                PriceBRL = singlePrice,
                PriceUSD = Math.Round(singlePrice / 4m, 2),
                Features =
                [
                    "1 análise completa com IA",
                    "Otimização para sites de vagas (Gupy, LinkedIn, Vagas.com, Trabalhar Brasil, Empregos.com.br, InfoJobs, Catho, Indeed)",
                    "Simulador de entrevista com IA",
                    "Currículo melhorado em PDF ou WORD",
                    "Palavras-chave estratégicas",
                    "Pesquisa de vagas de emprego",
                    "Análise única para um site específico"
                ]
            },
            ["pack3"] = new PricingPlan
            {
                Id = "pack3",
                Name = "Pacote 3 Análises",
                Description = "3 análises completas otimizadas para diferentes sites",
                Analyses = 3,
                PriceBRL = pack3Price,
                PriceUSD = Math.Round(pack3Price / 4m, 2),
                Savings = BuildSavingsLabel(3, config.CreditUnitPriceBRL, pack3Price) ?? "Melhor custo-benefício",
                Features =
                [
                    "3 análises completas com IA",
                    "Otimização para sites de vagas (Gupy, LinkedIn, Vagas.com, Trabalhar Brasil, Empregos.com.br, InfoJobs, Catho, Indeed)",
                    "Simulador de entrevista com IA",
                    "Currículo melhorado em PDF ou WORD",
                    "Palavras-chave estratégicas",
                    "Pesquisa de vagas de emprego"
                ]
            },
            ["pack5"] = new PricingPlan
            {
                Id = "pack5",
                Name = "Pacote 5 Análises",
                Description = "5 análises completas otimizadas para diferentes sites",
                Analyses = 5,
                PriceBRL = pack5Price,
                PriceUSD = Math.Round(pack5Price / 4m, 2),
                Savings = BuildSavingsLabel(5, config.CreditUnitPriceBRL, pack5Price),
                Features =
                [
                    "5 análises completas com IA",
                    "Otimização para sites de vagas (Gupy, LinkedIn, Vagas.com, Trabalhar Brasil, Empregos.com.br, InfoJobs, Catho, Indeed)",
                    "Simulador de entrevista com IA",
                    "Currículo melhorado em PDF ou WORD",
                    "Palavras-chave estratégicas",
                    "Pesquisa de vagas de emprego"
                ]
            },
            ["english"] = new PricingPlan
            {
                Id = "english",
                Name = "Currículo em Inglês",
                Description = "Currículo em inglês vinculado à análise — download em PDF e Word (ambos inclusos na compra)",
                Analyses = 0,
                PriceBRL = config.EnglishPriceBRL,
                PriceBRLBundle = config.EnglishBundlePriceBRL,
                PriceUSD = Math.Round(config.EnglishPriceBRL / 4m, 2),
                Features =
                [
                    "Currículo traduzido e adaptado para padrões internacionais",
                    "Formatação ATS-friendly",
                    "Download em PDF e Word (ambos inclusos)",
                    "Baixe quantas vezes quiser, em qualquer formato",
                    "Otimizado para vagas globais",
                    "Adaptação cultural profissional"
                ]
            }
        };
    }
}
