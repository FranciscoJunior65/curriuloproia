namespace CurriculosProIA.Domain.Entities;

public class PricingPlan
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Analyses { get; set; }
    public decimal PriceBRL { get; set; }
    public decimal PriceUSD { get; set; }
    public string? Savings { get; set; }
    public decimal? PriceBRLBundle { get; set; }
    public List<string> Features { get; set; } = new();
}
