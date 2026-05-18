using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

using CurriculosProIA.Service.Interfaces;
using CurriculosProIA.Repository.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CurriculosProIA.Service.Implementations;

public class JwtService : IJwtService
{
    private const string DefaultSecret = "seu_secret_key_super_seguro_aqui_mude_em_producao";
    private static readonly TimeSpan Expiry = TimeSpan.FromDays(30);

    private readonly string _secret;
    private readonly SymmetricSecurityKey _signingKey;
    private readonly ILogger<JwtService> _logger;

    public JwtService(IConfiguration configuration, ILogger<JwtService> logger)
    {
        _logger = logger;
        _secret = configuration["JWT_SECRET"]?.Trim() ?? DefaultSecret;
        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));

        _logger.LogInformation(
            "JWT configurado: {Configured} (tamanho: {Length})",
            !string.IsNullOrEmpty(configuration["JWT_SECRET"]),
            _secret.Length);
    }

    public string GenerateToken(string userId, string email)
    {
        var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Email, email),
            new Claim("userId", userId)
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.Add(Expiry),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = _signingKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        try
        {
            return handler.ValidateToken(token, parameters, out _);
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogDebug(ex, "Token JWT inválido ou expirado");
            return null;
        }
    }
}
