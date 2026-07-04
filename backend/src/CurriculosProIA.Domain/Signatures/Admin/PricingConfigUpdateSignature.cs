namespace CurriculosProIA.Domain.Signatures.Admin;

public class PricingConfigUpdateSignature
{
    public decimal? CreditUnitPriceBRL { get; set; }
    public decimal? SingleDiscountPercent { get; set; }
    public decimal? Pack3DiscountPercent { get; set; }
    public decimal? Pack5DiscountPercent { get; set; }
    public decimal? EnglishPriceBRL { get; set; }
    public decimal? EnglishBundlePriceBRL { get; set; }
    public decimal? TransactionFeeBRL { get; set; }
}
