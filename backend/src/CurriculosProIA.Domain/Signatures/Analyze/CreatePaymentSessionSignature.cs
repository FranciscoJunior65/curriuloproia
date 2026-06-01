namespace CurriculosProIA.Domain.Signatures.Analyze;

public class CreatePaymentSessionSignature
{
    public string? PlanId { get; set; }
    public string? UserId { get; set; }
    public string? Email { get; set; }
    public string? CouponCode { get; set; }
    public string? Cpf { get; set; }
    public bool? IncludeEnglish { get; set; }
    public string? AnalysisId { get; set; }
}
