namespace CurriculosProIA.Domain.Signatures.Purchase;

public class MockPurchaseSignature
{
    public string? UserId { get; set; }
    public string? PlanId { get; set; }
    public string? PlanName { get; set; }
    public int? CreditsAmount { get; set; }
    public decimal? Price { get; set; }
    public bool? IncludeEnglish { get; set; }
    public decimal? EnglishPrice { get; set; }
    public string? AnalysisId { get; set; }
    public string? CouponCode { get; set; }
    public string? Cpf { get; set; }
}
