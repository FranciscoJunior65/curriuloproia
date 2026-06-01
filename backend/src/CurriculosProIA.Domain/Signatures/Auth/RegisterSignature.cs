namespace CurriculosProIA.Domain.Signatures.Auth;

public class RegisterSignature
{
    public string? Email { get; set; }
    public string? Password { get; set; }
    public string? Name { get; set; }
    public string? Cpf { get; set; }
    public string? CupomCodigo { get; set; }
}

public class LinkPartnerCouponSignature
{
    public string? CupomCodigo { get; set; }
}
