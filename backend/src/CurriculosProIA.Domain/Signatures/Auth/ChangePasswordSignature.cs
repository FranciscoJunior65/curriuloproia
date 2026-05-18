namespace CurriculosProIA.Domain.Signatures.Auth;

public class ChangePasswordSignature
{
    public string? CurrentPassword { get; set; }
    public string? NewPassword { get; set; }
}
