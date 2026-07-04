namespace CurriculosProIA.Domain.Signatures.Admin;

public class AdminReconcileKiwifySignature
{
    public string OrderId { get; set; } = string.Empty;
    public string? PendingPurchaseId { get; set; }
}
