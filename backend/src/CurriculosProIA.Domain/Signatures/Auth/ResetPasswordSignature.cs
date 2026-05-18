namespace CurriculosProIA.Domain.Signatures.Auth;

public class ResetPasswordSignature
{
    public string? Token { get; set; }
    public string? NewPassword { get; set; }
}
