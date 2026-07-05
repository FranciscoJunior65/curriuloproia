namespace CurriculosProIA.Domain.Signatures.Admin;

public class AdminProcessKiwifyWebhookSignature
{
    /// <summary>JSON bruto do webhook Kiwify (mesmo formato enviado para /api/analyze/payment/kiwify/webhook).</summary>
    public string Payload { get; set; } = string.Empty;

    public string? PendingPurchaseId { get; set; }
}
