namespace CurriculosProIA.Domain.Signatures.Auth;

public class UpdateProfileSignature
{
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Cpf { get; set; }
    public string? DateOfBirth { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
}
