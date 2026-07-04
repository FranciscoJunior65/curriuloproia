namespace CurriculosProIA.Domain.Signatures.Analyze;

public class CaktoCardPaymentSignature : CreatePaymentSessionSignature
{
    public string? CustomerName { get; set; }
    public string? CardToken { get; set; }
    public string? AntifraudProfilingAttemptReference { get; set; }
    public string? Cavv { get; set; }
    public string? Eci { get; set; }
    public string? Xid { get; set; }
    public string? ReferenceId { get; set; }
    public string? Version { get; set; }
    public string? TransStatus { get; set; }
    public string? TdsServerTransId { get; set; }
    /// <summary>Quando true, cobra com paymentMethod credit_card (sem bloco threeDSecure).</summary>
    public bool? SkipThreeDs { get; set; }
}

public class CaktoPixPaymentSignature : CreatePaymentSessionSignature
{
    public string? CustomerName { get; set; }
    public string? AntifraudProfilingAttemptReference { get; set; }
}
