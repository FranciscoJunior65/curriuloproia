namespace CurriculosProIA.Domain.Entities;

public class UserProfile
{
    public string Id { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Cpf { get; set; }
    public string? DateOfBirth { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public int Credits { get; set; }
    public string? Plan { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public DateTimeOffset? LastAnalysis { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public bool EmailVerified { get; set; }
    public string? VerificationCode { get; set; }
    public DateTimeOffset? VerificationCodeExpiresAt { get; set; }
    public string UserType { get; set; } = "cliente";
    public string? PasswordHash { get; set; }
}
