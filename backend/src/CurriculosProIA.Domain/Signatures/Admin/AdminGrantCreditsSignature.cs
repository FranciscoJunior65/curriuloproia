namespace CurriculosProIA.Domain.Signatures.Admin;

public class AdminGrantCreditsSignature
{
    public string? UserId { get; set; }
    public string? Email { get; set; }
    public string? PlanId { get; set; }
    public int? Credits { get; set; }
    public decimal? Price { get; set; }
    public string? PaymentMethod { get; set; }
    public string? PaymentId { get; set; }
    public string? Reason { get; set; }
    public bool SendEmail { get; set; } = true;
}
