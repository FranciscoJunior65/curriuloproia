namespace CurriculosProIA.Domain.Signatures.Analyze;

public class MercadoPagoCardPaymentSignature : CreatePaymentSessionSignature
{
    public string? Token { get; set; }
    public string? PaymentMethodId { get; set; }
    public string? IssuerId { get; set; }
    public int Installments { get; set; } = 1;
}
