namespace CurriculosProIA.Domain.Dtos;

public class KiwifySaleDetailsDto
{
    public string OrderId { get; set; } = string.Empty;
    public string? OrderRef { get; set; }
    public string? Status { get; set; }
    public bool Paid { get; set; }
    public bool AlreadyFulfilled { get; set; }
    public string? CustomerEmail { get; set; }
    public decimal PriceBRL { get; set; }
    public string? ExternalReference { get; set; }
    public string? PaymentIdUsed { get; set; }
}

public class PendingPurchaseDto
{
    public string Id { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? UserEmail { get; set; }
    public string? PlanId { get; set; }
    public string? PlanName { get; set; }
    public int CreditsAmount { get; set; }
    public decimal? Price { get; set; }
    public string? PaymentMethod { get; set; }
    public string? PaymentId { get; set; }
    public string Status { get; set; } = "pendente";
    public DateTimeOffset? CreatedAt { get; set; }
}
