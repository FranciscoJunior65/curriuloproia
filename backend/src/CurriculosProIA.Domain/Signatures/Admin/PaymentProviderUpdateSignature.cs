namespace CurriculosProIA.Domain.Signatures.Admin;

public class PaymentProviderUpdateSignature
{
    public string? Provider { get; set; }
    /// <summary>Ambiente Mercado Pago: test | production</summary>
    public string? MercadoPagoMode { get; set; }
}
