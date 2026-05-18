using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace CurriculosProIA.Api.Helpers;

public static class JwtAuthHelper
{
    public static string? TryGetUserId(IHeaderDictionary headers, IConfiguration configuration)
    {
        var authHeader = headers.Authorization.FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var token = authHeader["Bearer ".Length..].Trim();
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        try
        {
            var secret = configuration["JWT_SECRET"] ?? "seu_secret_key_super_seguro_aqui_mude_em_producao";
            var handler = new JwtSecurityTokenHandler();
            var validation = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                ClockSkew = TimeSpan.FromMinutes(1)
            };

            var principal = handler.ValidateToken(token, validation, out _);
            var userId = principal.FindFirst("userId")?.Value;
            return string.IsNullOrEmpty(userId) ? null : userId;
        }
        catch
        {
            return null;
        }
    }
}
