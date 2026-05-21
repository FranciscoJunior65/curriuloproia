namespace CurriculosProIA.Domain.Dtos;

public class PricingConfigDto
{
    public decimal CreditUnitPriceBRL { get; set; } = 7.90m;
    public decimal SingleDiscountPercent { get; set; }
    public decimal Pack3DiscountPercent { get; set; }
    public decimal Pack5DiscountPercent { get; set; }
    public decimal EnglishPriceBRL { get; set; } = 17.90m;
    public decimal EnglishBundlePriceBRL { get; set; } = 5.90m;

    public decimal? SinglePriceOverride { get; set; }
    public decimal? Pack3PriceOverride { get; set; }
    public decimal? Pack5PriceOverride { get; set; }

    public decimal SinglePriceBRL => SinglePriceOverride ?? ComputePlanPrice(1, SingleDiscountPercent);
    public decimal Pack3PriceBRL => Pack3PriceOverride ?? ComputePlanPrice(3, Pack3DiscountPercent);
    public decimal Pack5PriceBRL => Pack5PriceOverride ?? ComputePlanPrice(5, Pack5DiscountPercent);

    public decimal ComputePlanPrice(int analyses, decimal discountPercent)
    {
        if (analyses <= 0)
        {
            return 0;
        }

        var basePrice = CreditUnitPriceBRL * analyses;
        var factor = 1m - (discountPercent / 100m);
        return Math.Round(Math.Max(0, basePrice * factor), 2);
    }
}
