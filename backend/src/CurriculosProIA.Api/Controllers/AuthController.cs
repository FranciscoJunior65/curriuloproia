using System.Security.Claims;
using CurriculosProIA.App.Interfaces;
using CurriculosProIA.Domain.Signatures.Auth;
using CurriculosProIA.Repository.Interfaces;
using CurriculosProIA.Service.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CurriculosProIA.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthAppService _auth;
    private readonly IUserProfileRepository _users;
    private readonly IJwtService _jwt;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthAppService auth,
        IUserProfileRepository users,
        IJwtService jwt,
        IConfiguration configuration,
        ILogger<AuthController> logger)
    {
        _auth = auth;
        _users = users;
        _jwt = jwt;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("register")]
    public Task<IActionResult> Register([FromBody] RegisterSignature request, CancellationToken ct) =>
        _auth.Register(request, ct);

    [HttpPost("login")]
    public Task<IActionResult> Login([FromBody] LoginSignature request, CancellationToken ct) =>
        _auth.Login(request, ct);

    [HttpPost("verify-email")]
    public Task<IActionResult> VerifyEmail([FromBody] EmailCodeSignature request, CancellationToken ct) =>
        _auth.VerifyEmail(request, ct);

    [HttpPost("resend-verification")]
    public Task<IActionResult> ResendVerification([FromBody] EmailOnlySignature request, CancellationToken ct) =>
        _auth.ResendVerification(request, ct);

    [HttpGet("verify")]
    [Authorize]
    public Task<IActionResult> Verify(CancellationToken ct) => _auth.Verify(ct);

    [HttpGet("verify-email-link")]
    public Task<IActionResult> VerifyEmailLink([FromQuery] string? email, [FromQuery] string? token, CancellationToken ct) =>
        _auth.VerifyEmailLink(email, token, ct);

    [HttpPost("change-password")]
    [Authorize]
    public Task<IActionResult> ChangePassword([FromBody] ChangePasswordSignature request, CancellationToken ct) =>
        _auth.ChangePassword(request, ct);

    [HttpPatch("profile")]
    [Authorize]
    public Task<IActionResult> UpdateProfile([FromBody] UpdateProfileSignature request, CancellationToken ct) =>
        _auth.UpdateProfile(request, ct);

    [HttpPost("forgot-password")]
    public Task<IActionResult> ForgotPassword([FromBody] EmailOnlySignature request, CancellationToken ct) =>
        _auth.ForgotPassword(request, ct);

    [HttpPost("reset-password")]
    public Task<IActionResult> ResetPassword([FromBody] ResetPasswordSignature request, CancellationToken ct) =>
        _auth.ResetPassword(request, ct);

    [HttpPost("request-login-code")]
    public Task<IActionResult> RequestLoginCode([FromBody] EmailOnlySignature request, CancellationToken ct) =>
        _auth.RequestLoginCode(request, ct);

    [HttpPost("verify-login-code")]
    public Task<IActionResult> VerifyLoginCode([FromBody] EmailCodeSignature request, CancellationToken ct) =>
        _auth.VerifyLoginCode(request, ct);

    [HttpPost("link-partner-coupon")]
    [Authorize]
    public Task<IActionResult> LinkPartnerCoupon([FromBody] LinkPartnerCouponSignature request, CancellationToken ct) =>
        _auth.LinkPartnerCoupon(request, ct);

    [HttpGet("referral-coupon")]
    [Authorize]
    public Task<IActionResult> GetReferralCoupon(CancellationToken ct) =>
        _auth.GetReferralCoupon(ct);

    [HttpPost("delete-account")]
    [Authorize]
    public Task<IActionResult> DeleteAccount([FromBody] DeleteAccountSignature body, CancellationToken ct) =>
        _auth.DeleteAccount(body, ct);

    [HttpGet("google")]
    public IActionResult GoogleLogin()
    {
        var frontendUrl = GetFrontendUrl();
        if (!IsGoogleConfigured())
            return Redirect($"{frontendUrl}/login?error=google_not_configured");

        var redirectUrl = Url.Action(nameof(GoogleCallback), "Auth", null, Request.Scheme);
        var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    [HttpGet("google/callback")]
    public async Task<IActionResult> GoogleCallback(CancellationToken cancellationToken)
    {
        var frontendUrl = GetFrontendUrl();
        if (!IsGoogleConfigured())
            return Redirect($"{frontendUrl}/login?error=google_not_configured");

        var authResult = await HttpContext.AuthenticateAsync(GoogleDefaults.AuthenticationScheme);
        if (!authResult.Succeeded || authResult.Principal == null)
            return Redirect($"{frontendUrl}/login?error=google_auth_failed");

        try
        {
            var email = authResult.Principal.FindFirstValue(ClaimTypes.Email)
                ?? authResult.Principal.FindFirstValue("email");
            var name = authResult.Principal.FindFirstValue(ClaimTypes.Name)
                ?? authResult.Principal.FindFirstValue("name")
                ?? string.Empty;

            if (string.IsNullOrEmpty(email))
                return Redirect($"{frontendUrl}/login?error=google_auth_failed");

            var userProfile = await _users.GetUserProfileByEmailAsync(email, includePassword: false, cancellationToken);
            if (userProfile == null)
            {
                var userId = Guid.NewGuid().ToString();
                userProfile = await _users.GetOrCreateUserProfileAsync(
                    userId, email, name, passwordHash: null, emailVerified: true,
                    verificationCode: null, cancellationToken: cancellationToken);
            }
            else if (!userProfile.EmailVerified)
            {
                userProfile = await _users.UpdateUserProfileAsync(userProfile.Id,
                    new Dictionary<string, object?> { ["email_verified"] = true }, cancellationToken);
            }

            var token = _jwt.GenerateToken(userProfile.Id, userProfile.Email!);
            return Redirect($"{frontendUrl}/login?token={token}&success=true");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro no callback do Google");
            return Redirect($"{frontendUrl}/login?error=google_auth_error");
        }
    }

    private string GetFrontendUrl() =>
        _configuration["FRONTEND_URL"]?.Trim() ?? "http://localhost:4200";

    private bool IsGoogleConfigured() =>
        !string.IsNullOrWhiteSpace(_configuration["GOOGLE_CLIENT_ID"]) &&
        !string.IsNullOrWhiteSpace(_configuration["GOOGLE_CLIENT_SECRET"]) &&
        !_configuration["GOOGLE_CLIENT_ID"]!.Contains("seu-google-client-id", StringComparison.OrdinalIgnoreCase);
}
