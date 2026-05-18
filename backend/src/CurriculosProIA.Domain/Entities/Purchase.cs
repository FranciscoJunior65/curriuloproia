namespace CurriculosProIA.Domain.Entities;

public class Purchase
{
    public string Id { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? PlanId { get; set; }
    public string? PlanName { get; set; }
    public int CreditsAmount { get; set; }
    public decimal? Price { get; set; }
    public string Currency { get; set; } = "BRL";
    public string Status { get; set; } = "concluida";
    public string? PaymentMethod { get; set; }
    public string? PaymentId { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? ParentPurchaseId { get; set; }
    public string ServiceType { get; set; } = "analysis_plan";
    public string? CouponId { get; set; }
    public string? CouponName { get; set; }
    public decimal? DiscountPercent { get; set; }
    public decimal? OriginalPrice { get; set; }
}
