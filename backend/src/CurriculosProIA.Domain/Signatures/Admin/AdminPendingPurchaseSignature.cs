namespace CurriculosProIA.Domain.Signatures.Admin;

public class AdminPendingPurchaseSignature
{
    public string? UserId { get; set; }
    public string? Email { get; set; }
    public string PlanId { get; set; } = string.Empty;
    public string? KiwifyOrderId { get; set; }
}
