using System.Security.Claims;

namespace CurriculosProIA.Service.Interfaces;

public interface IJwtService
{
    string GenerateToken(string userId, string email);
    ClaimsPrincipal? ValidateToken(string token);
}
