using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using CurriculosProIA.Repository.Interfaces;
using CurriculosProIA.Service.Interfaces;
using CurriculosProIA.Domain.Entities;
using CurriculosProIA.Domain.Dtos;
using CurriculosProIA.Domain.Signatures.Auth;
using CurriculosProIA.Domain.Signatures.Analyze;
using CurriculosProIA.Domain.Signatures.Admin;
using CurriculosProIA.Domain.Signatures.Purchase;
using CurriculosProIA.App.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.RegularExpressions;


using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using CurriculosProIA.App;
using CurriculosProIA.App.Interfaces;

namespace CurriculosProIA.App.Implementations;

public class AuthAppService : AppControllerBase, IAuthAppService 
{
    private readonly IHttpContextAccessor _http;
    private static readonly Regex EmailRegex = new(@"^[^\s@]+@[^\s@]+\.[^\s@]+$", RegexOptions.Compiled);

    private readonly IAppDataStore _data;
    private readonly IJwtService _jwt;
    private readonly IEmailService _email;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthAppService> _logger;
    private readonly IWebHostEnvironment _environment;

    public AuthAppService(
        IAppDataStore data,
        IJwtService jwt,
        IEmailService email,
        IConfiguration configuration,
        ILogger<AuthAppService> logger,
        IWebHostEnvironment environment,
        IHttpContextAccessor http)
    {
        _http = http;
        _data = data;
        _jwt = jwt;
        _email = email;
        _configuration = configuration;
        _logger = logger;
        _environment = environment;
    }

        public async Task<IActionResult> Register(RegisterSignature request, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { success = false, error = "Email e senha são obrigatórios" });
            }

            if (!EmailRegex.IsMatch(request.Email))
            {
                return BadRequest(new { success = false, error = "Email inválido" });
            }

            if (request.Password.Length < 6)
            {
                return BadRequest(new { success = false, error = "Senha deve ter no mínimo 6 caracteres" });
            }

            var cpfNorm = request.Cpf != null ? Regex.Replace(request.Cpf, @"\D", string.Empty) : string.Empty;
            if (cpfNorm.Length != 11)
            {
                return BadRequest(new { success = false, error = "CPF é obrigatório e deve conter 11 dígitos" });
            }

            var existingProfile = await _data.GetUserProfileByEmailAsync(request.Email, includePassword: false, cancellationToken);

            if (existingProfile != null)
            {
                if (existingProfile.EmailVerified)
                {
                    return Conflict(new
                    {
                        success = false,
                        error = "Email já cadastrado",
                        message = "Este email já está cadastrado e verificado. Faça login para continuar.",
                        action = "login"
                    });
                }

                var verificationToken = Guid.NewGuid().ToString();
                await _data.UpdateVerificationTokenAsync(existingProfile.Id, verificationToken, cancellationToken: cancellationToken);

                try
                {
                    await _email.SendVerificationLinkEmailAsync(request.Email, verificationToken, existingProfile.Name, cancellationToken);
                }
                catch (Exception emailError)
                {
                    _logger.LogError(emailError, "Erro ao enviar email");
                }

                return Conflict(new
                {
                    success = false,
                    error = "Email já cadastrado",
                    message = "Este email já está cadastrado mas não foi verificado. Enviamos um novo link de verificação para seu email.",
                    requiresVerification = true,
                    action = "verify"
                });
            }

            var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 10);
            var verificationCode = _email.GenerateVerificationCode();
            var userId = Guid.NewGuid().ToString();

            var user = await _data.GetOrCreateUserProfileAsync(
                userId,
                request.Email,
                request.Name ?? string.Empty,
                passwordHash,
                emailVerified: false,
                verificationCode: verificationCode,
                cpf: cpfNorm,
                cancellationToken: cancellationToken);

            if (!string.IsNullOrWhiteSpace(request.CupomCodigo))
            {
                try
                {
                    await _data.RegisterPartnerReferralAsync(user.Id, request.CupomCodigo.Trim(), cancellationToken);
                }
                catch (Exception referralError)
                {
                    _logger.LogWarning(referralError, "Não foi possível vincular cupom de parceiro no cadastro");
                }
            }

            try
            {
                await _email.SendVerificationEmailAsync(request.Email, verificationCode, request.Name ?? string.Empty, cancellationToken);
            }
            catch (Exception emailError)
            {
                _logger.LogError(emailError, "Erro ao enviar email");
            }

            return Ok(new
            {
                success = true,
                message = "Conta criada! Verifique seu email para o código de verificação.",
                requiresVerification = true,
                userId = user.Id,
                email = user.Email
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar conta");
            return StatusCode(500, new
            {
                success = false,
                error = "Erro ao criar conta",
                message = ex.Message
            });
        }
    }

        public async Task<IActionResult> Login(LoginSignature request, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { success = false, error = "Email e senha são obrigatórios" });
            }

            var profile = await _data.GetUserProfileByEmailAsync(request.Email, includePassword: true, cancellationToken);
            if (profile == null)
            {
                return Unauthorized(new { success = false, error = "Email ou senha incorretos" });
            }

            var isValidPassword = await _data.VerifyUserPasswordAsync(request.Email, request.Password, cancellationToken);
            if (!isValidPassword)
            {
                return Unauthorized(new { success = false, error = "Email ou senha incorretos" });
            }

            if (!profile.EmailVerified)
            {
                var verificationCode = _email.GenerateVerificationCode();
                await _data.UpdateVerificationCodeAsync(profile.Id, verificationCode, cancellationToken: cancellationToken);

                try
                {
                    await _email.SendVerificationEmailAsync(request.Email, verificationCode, profile.Name, cancellationToken);
                }
                catch (Exception emailError)
                {
                    _logger.LogError(emailError, "Erro ao enviar email");
                }

                return StatusCode(403, new
                {
                    success = false,
                    error = "Email não verificado",
                    requiresVerification = true,
                    message = "Sua senha está correta, mas o email ainda não foi verificado. Enviamos um novo código de verificação para seu email.",
                    codeSent = true
                });
            }

            var token = _jwt.GenerateToken(profile.Id, profile.Email!);

            try
            {
                await _email.SendLoginNotificationEmailAsync(profile.Email!, profile.Name, cancellationToken);
            }
            catch (Exception emailError)
            {
                _logger.LogError(emailError, "Erro ao enviar email de notificação de login");
            }

            return Ok(new
            {
                success = true,
                message = "Login realizado com sucesso",
                token,
                user = MapUser(profile)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao fazer login");
            var message = _environment.IsProduction()
                ? "Tente novamente ou contate o suporte."
                : ex.Message;
            return StatusCode(500, new
            {
                success = false,
                error = "Erro ao fazer login",
                message
            });
        }
    }

        public async Task<IActionResult> VerifyEmail(EmailCodeSignature request, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Code))
            {
                return BadRequest(new { success = false, error = "Email e código são obrigatórios" });
            }

            var profile = await _data.VerifyEmailCodeAsync(request.Email, request.Code, cancellationToken);
            var token = _jwt.GenerateToken(profile.Id, profile.Email!);

            try
            {
                await _email.SendWelcomeEmailAsync(profile.Email!, profile.Name, cancellationToken);
            }
            catch (Exception emailError)
            {
                _logger.LogError(emailError, "Erro ao enviar email de boas-vindas");
            }

            return Ok(new
            {
                success = true,
                message = "Email verificado com sucesso!",
                token,
                user = MapUser(profile)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar email");
            return BadRequest(new
            {
                success = false,
                error = ex.Message,
                message = ex.Message
            });
        }
    }

        public async Task<IActionResult> ResendVerification(EmailOnlySignature request, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest(new { success = false, error = "Email é obrigatório" });
            }

            var profile = await _data.GetUserProfileByEmailAsync(request.Email, includePassword: false, cancellationToken);
            if (profile == null)
            {
                return NotFound(new { success = false, error = "Usuário não encontrado" });
            }

            if (profile.EmailVerified)
            {
                return BadRequest(new { success = false, error = "Email já está verificado" });
            }

            var verificationCode = _email.GenerateVerificationCode();
            await _data.UpdateVerificationCodeAsync(profile.Id, verificationCode, cancellationToken: cancellationToken);

            try
            {
                await _email.SendVerificationEmailAsync(request.Email, verificationCode, profile.Name, cancellationToken);
                return Ok(new { success = true, message = "Código de verificação reenviado com sucesso!" });
            }
            catch (Exception emailError)
            {
                _logger.LogError(emailError, "Erro ao enviar email");
                return StatusCode(500, new
                {
                    success = false,
                    error = "Erro ao enviar email de verificação",
                    message = emailError.Message
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao reenviar código");
            return StatusCode(500, new
            {
                success = false,
                error = "Erro ao reenviar código",
                message = ex.Message
            });
        }
    }

        public async Task<IActionResult> Verify(CancellationToken cancellationToken)
    {
        try
        {
            var token = ExtractBearerToken();
            if (string.IsNullOrEmpty(token))
            {
                return Unauthorized(new { success = false, error = "Token não fornecido" });
            }

            var principal = _jwt.ValidateToken(token);
            if (principal == null)
            {
                return Unauthorized(new { success = false, error = "Token inválido ou expirado" });
            }

            var userId = GetUserId(principal);
            var profile = await _data.GetUserProfileAsync(userId, cancellationToken);
            if (profile == null)
            {
                return Unauthorized(new { success = false, error = "Usuário não encontrado" });
            }

            return Ok(new
            {
                success = true,
                user = MapUser(profile, includePlan: true),
                referralCoupon = await GetReferralCouponForUserAsync(userId, cancellationToken)
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                error = "Erro ao verificar token",
                message = ex.Message
            });
        }
    }

    public async Task<IActionResult> LinkPartnerCoupon(
        LinkPartnerCouponSignature request,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetAuthenticatedUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { success = false, error = "Não autenticado" });
            }

            if (string.IsNullOrWhiteSpace(request.CupomCodigo))
            {
                return BadRequest(new { success = false, error = "Código do cupom é obrigatório" });
            }

            if (await _data.UserHasPartnerReferralAsync(userId, cancellationToken))
            {
                var existing = await _data.GetPartnerReferralByUserIdAsync(userId, cancellationToken);
                return Ok(new
                {
                    success = true,
                    alreadyLinked = true,
                    message = "Sua conta já possui um cupom vinculado.",
                    referralCoupon = existing
                });
            }

            await _data.RegisterPartnerReferralAsync(userId, request.CupomCodigo.Trim(), cancellationToken);
            var referral = await _data.GetPartnerReferralByUserIdAsync(userId, cancellationToken);

            return Ok(new
            {
                success = true,
                message = "Cupom vinculado à sua conta.",
                referralCoupon = referral
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao vincular cupom de parceiro");
            return StatusCode(500, new { success = false, error = "Erro ao vincular cupom", message = ex.Message });
        }
    }

    public async Task<IActionResult> GetReferralCoupon(CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetAuthenticatedUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { success = false, error = "Não autenticado" });
            }

            var referral = await GetReferralCouponForUserAsync(userId, cancellationToken);
            return Ok(new { success = true, referralCoupon = referral });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, error = "Erro ao obter cupom vinculado", message = ex.Message });
        }
    }

    private async Task<object?> GetReferralCouponForUserAsync(string userId, CancellationToken cancellationToken)
    {
        var referral = await _data.GetPartnerReferralByUserIdAsync(userId, cancellationToken);
        if (referral == null)
        {
            return null;
        }

        return new
        {
            couponId = referral.CouponId,
            couponCode = referral.CouponCode,
            discountPercent = referral.DiscountPercent,
            partnerId = referral.PartnerId,
            partnerName = referral.PartnerName,
            linkedAt = referral.LinkedAt
        };
    }

        public async Task<IActionResult> VerifyEmailLink(
        string? email,
        string? token,
        CancellationToken cancellationToken)
    {
        var frontendUrl = GetFrontendUrl();

        try
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
            {
                return BadRequest(new { success = false, error = "Email e token são obrigatórios" });
            }

            var profile = await _data.VerifyEmailTokenAsync(email, token, cancellationToken);
            var jwtToken = _jwt.GenerateToken(profile.Id, profile.Email!);

            try
            {
                await _email.SendWelcomeEmailAsync(profile.Email!, profile.Name, cancellationToken);
            }
            catch (Exception emailError)
            {
                _logger.LogError(emailError, "Erro ao enviar email de boas-vindas");
            }

            return Redirect($"{frontendUrl}/verify-email-success?token={jwtToken}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar email por token");
            return Redirect($"{frontendUrl}/verify-email-error?error={Uri.EscapeDataString(ex.Message)}");
        }
    }

            public async Task<IActionResult> ChangePassword(ChangePasswordSignature request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetAuthenticatedUserId();
            if (userId == null)
            {
                return Unauthorized(new { success = false, error = "Token não fornecido" });
            }

            if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return BadRequest(new { success = false, error = "Senha atual e nova senha são obrigatórias" });
            }

            if (request.NewPassword.Length < 6)
            {
                return BadRequest(new { success = false, error = "Nova senha deve ter no mínimo 6 caracteres" });
            }

            var profile = await _data.GetUserProfileAsync(userId, cancellationToken);
            if (profile == null)
            {
                return NotFound(new { success = false, error = "Usuário não encontrado" });
            }

            var isValidPassword = await _data.VerifyUserPasswordAsync(profile.Email!, request.CurrentPassword, cancellationToken);
            if (!isValidPassword)
            {
                return Unauthorized(new { success = false, error = "Senha atual incorreta" });
            }

            var newPasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, workFactor: 10);
            await _data.UpdateUserProfileAsync(userId, new Dictionary<string, object?>
            {
                ["password_hash"] = newPasswordHash
            }, cancellationToken);

            return Ok(new { success = true, message = "Senha alterada com sucesso" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao trocar senha");
            return StatusCode(500, new
            {
                success = false,
                error = "Erro ao trocar senha",
                message = ex.Message
            });
        }
    }

    [HttpPatch("profile")]
        public async Task<IActionResult> UpdateProfile(UpdateProfileSignature request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetAuthenticatedUserId();
            if (userId == null)
            {
                return Unauthorized(new { success = false, error = "Token não fornecido" });
            }

            var profile = await _data.GetUserProfileAsync(userId, cancellationToken);
            if (profile == null)
            {
                return NotFound(new { success = false, error = "Usuário não encontrado" });
            }

            var updates = new Dictionary<string, object?>();

            if (request.Name != null)
            {
                updates["name"] = request.Name.Trim();
            }

            if (request.Email != null)
            {
                var emailTrim = request.Email.Trim();
                if (string.IsNullOrEmpty(emailTrim))
                {
                    return BadRequest(new { success = false, error = "Email não pode ser vazio" });
                }

                if (!EmailRegex.IsMatch(emailTrim))
                {
                    return BadRequest(new { success = false, error = "Email inválido" });
                }

                if (!string.Equals(emailTrim, profile.Email, StringComparison.OrdinalIgnoreCase))
                {
                    var existing = await _data.GetUserProfileByEmailAsync(emailTrim, includePassword: false, cancellationToken);
                    if (existing != null && existing.Id != userId)
                    {
                        return Conflict(new
                        {
                            success = false,
                            error = "Este email já está em uso por outra conta"
                        });
                    }
                }

                updates["email"] = emailTrim;
            }

            if (request.Cpf != null)
            {
                var cpfNorm = Regex.Replace(request.Cpf, @"\D", string.Empty);
                if (cpfNorm.Length != 11)
                {
                    return BadRequest(new { success = false, error = "CPF deve conter 11 dígitos (apenas números)" });
                }

                updates["cpf"] = cpfNorm;
            }

            if (request.DateOfBirth != null)
            {
                updates["date_of_birth"] = string.IsNullOrWhiteSpace(request.DateOfBirth)
                    ? null
                    : request.DateOfBirth.Trim().Split('T')[0];
            }

            if (request.City != null)
            {
                updates["city"] = string.IsNullOrWhiteSpace(request.City) ? null : request.City.Trim();
            }

            if (request.Country != null)
            {
                updates["country"] = string.IsNullOrWhiteSpace(request.Country) ? null : request.Country.Trim();
            }

            if (updates.Count == 0)
            {
                return BadRequest(new
                {
                    success = false,
                    error = "Envie pelo menos um campo para atualizar (name, email, cpf, date_of_birth, city, country)"
                });
            }

            var updated = await _data.UpdateUserProfileAsync(userId, updates, cancellationToken);
            return Ok(new
            {
                success = true,
                message = "Dados atualizados com sucesso",
                user = MapUser(updated, includePlan: true)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar perfil");
            return StatusCode(500, new
            {
                success = false,
                error = "Erro ao atualizar perfil",
                message = ex.Message
            });
        }
    }

        public async Task<IActionResult> ForgotPassword(EmailOnlySignature request, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest(new { success = false, error = "Email é obrigatório" });
            }

            var profile = await _data.GetUserProfileByEmailAsync(request.Email, includePassword: false, cancellationToken);
            if (profile != null)
            {
                var resetToken = Guid.NewGuid().ToString();
                await _data.UpdateVerificationTokenAsync(profile.Id, resetToken, expiresInHours: 1, cancellationToken: cancellationToken);

                try
                {
                    await _email.SendPasswordResetEmailAsync(profile.Email!, resetToken, profile.Name, cancellationToken);
                }
                catch (Exception emailError)
                {
                    _logger.LogError(emailError, "Erro ao enviar email de recuperação");
                }
            }

            return Ok(new
            {
                success = true,
                message = "Se o email estiver cadastrado, você receberá um link de recuperação."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao solicitar recuperação de senha");
            return StatusCode(500, new
            {
                success = false,
                error = "Erro ao processar solicitação",
                message = ex.Message
            });
        }
    }

        public async Task<IActionResult> ResetPassword(ResetPasswordSignature request, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return BadRequest(new { success = false, error = "Token e nova senha são obrigatórios" });
            }

            if (request.NewPassword.Length < 6)
            {
                return BadRequest(new { success = false, error = "Nova senha deve ter no mínimo 6 caracteres" });
            }

            var profile = await _data.GetUserByResetTokenAsync(request.Token, cancellationToken);
            var newPasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, workFactor: 10);

            await _data.UpdateUserProfileAsync(profile.Id, new Dictionary<string, object?>
            {
                ["password_hash"] = newPasswordHash
            }, cancellationToken);

            _logger.LogInformation(
                "Senha redefinida via recuperação para {Email} (ID: {UserId})",
                profile.Email,
                profile.Id);

            try
            {
                await _email.SendPasswordChangeNotificationEmailAsync(profile.Email!, profile.Name, cancellationToken);
            }
            catch (Exception emailError)
            {
                _logger.LogError(emailError, "Erro ao enviar email de notificação de mudança de senha");
            }

            await _data.UpdateUserProfileAsync(profile.Id, new Dictionary<string, object?>
            {
                ["verification_code"] = null,
                ["verification_code_expires_at"] = null
            }, cancellationToken);

            return Ok(new
            {
                success = true,
                message = "Senha redefinida com sucesso! Faça login com sua nova senha."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao redefinir senha");
            return BadRequest(new
            {
                success = false,
                error = ex.Message,
                message = ex.Message
            });
        }
    }

        public async Task<IActionResult> RequestLoginCode(EmailOnlySignature request, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest(new { success = false, error = "Email é obrigatório" });
            }

            var profile = await _data.GetUserProfileByEmailAsync(request.Email, includePassword: false, cancellationToken);
            if (profile != null)
            {
                var loginCode = _email.GenerateVerificationCode();
                await _data.UpdateVerificationCodeAsync(profile.Id, loginCode, expiresInMinutes: 10, cancellationToken: cancellationToken);

                try
                {
                    await _email.SendLoginCodeEmailAsync(profile.Email!, loginCode, profile.Name, cancellationToken);
                }
                catch (Exception emailError)
                {
                    _logger.LogError(emailError, "Erro ao enviar email com código de login");
                }
            }

            return Ok(new
            {
                success = true,
                message = "Se o email estiver cadastrado, você receberá um código de login."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao solicitar código de login");
            return StatusCode(500, new
            {
                success = false,
                error = "Erro ao processar solicitação",
                message = ex.Message
            });
        }
    }

        public async Task<IActionResult> VerifyLoginCode(EmailCodeSignature request, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Code))
            {
                return BadRequest(new { success = false, error = "Email e código são obrigatórios" });
            }

            var profile = await _data.VerifyLoginCodeAsync(request.Email, request.Code, cancellationToken);
            var token = _jwt.GenerateToken(profile.Id, profile.Email!);

            try
            {
                await _email.SendLoginNotificationEmailAsync(profile.Email!, profile.Name, cancellationToken);
            }
            catch (Exception emailError)
            {
                _logger.LogError(emailError, "Erro ao enviar email de notificação de login");
            }

            return Ok(new
            {
                success = true,
                message = "Login realizado com sucesso",
                token,
                user = MapUser(profile)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar código de login");
            return BadRequest(new
            {
                success = false,
                error = ex.Message,
                message = ex.Message
            });
        }
    }

    private string? ExtractBearerToken()
    {
        var authHeader = _http.HttpContext!.Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return authHeader["Bearer ".Length..].Trim();
    }

    private string? GetAuthenticatedUserId()
    {
        var user = _http.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            return user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("userId");
        }

        var token = ExtractBearerToken();
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        var principal = _jwt.ValidateToken(token);
        return principal == null ? null : GetUserId(principal);
    }

    private static string GetUserId(ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? principal.FindFirstValue("userId")
        ?? throw new InvalidOperationException("userId ausente no token");

    private string GetFrontendUrl() =>
        _configuration["FRONTEND_URL"]?.Trim() ?? "http://localhost:4200";

    private bool IsGoogleConfigured() =>
        !string.IsNullOrWhiteSpace(_configuration["GOOGLE_CLIENT_ID"]) &&
        !string.IsNullOrWhiteSpace(_configuration["GOOGLE_CLIENT_SECRET"]) &&
        !_configuration["GOOGLE_CLIENT_ID"]!.Contains("seu-google-client-id", StringComparison.OrdinalIgnoreCase);

    private static object MapUser(UserProfile profile, bool includePlan = false)
    {
        if (includePlan)
        {
            return new
            {
                id = profile.Id,
                email = profile.Email,
                name = profile.Name,
                cpf = profile.Cpf,
                date_of_birth = profile.DateOfBirth,
                city = profile.City,
                country = profile.Country,
                credits = profile.Credits,
                plan = profile.Plan,
                user_type = profile.UserType ?? "cliente"
            };
        }

        return new
        {
            id = profile.Id,
            email = profile.Email,
            name = profile.Name,
            cpf = profile.Cpf,
            date_of_birth = profile.DateOfBirth,
            city = profile.City,
            country = profile.Country,
            credits = profile.Credits,
            user_type = profile.UserType ?? "cliente"
        };
    }
}
