using CurriculosProIA.Domain.Signatures.Auth;
using Microsoft.AspNetCore.Mvc;

namespace CurriculosProIA.App.Interfaces;

public interface IAuthAppService
{
    Task<IActionResult> Register(RegisterSignature request, CancellationToken cancellationToken = default);
    Task<IActionResult> Login(LoginSignature request, CancellationToken cancellationToken = default);
    Task<IActionResult> VerifyEmail(EmailCodeSignature request, CancellationToken cancellationToken = default);
    Task<IActionResult> ResendVerification(EmailOnlySignature request, CancellationToken cancellationToken = default);
    Task<IActionResult> Verify(CancellationToken cancellationToken = default);
    Task<IActionResult> VerifyEmailLink(string? email, string? token, CancellationToken cancellationToken = default);
    Task<IActionResult> ChangePassword(ChangePasswordSignature request, CancellationToken cancellationToken = default);
    Task<IActionResult> UpdateProfile(UpdateProfileSignature request, CancellationToken cancellationToken = default);
    Task<IActionResult> ForgotPassword(EmailOnlySignature request, CancellationToken cancellationToken = default);
    Task<IActionResult> ResetPassword(ResetPasswordSignature request, CancellationToken cancellationToken = default);
    Task<IActionResult> RequestLoginCode(EmailOnlySignature request, CancellationToken cancellationToken = default);
    Task<IActionResult> VerifyLoginCode(EmailCodeSignature request, CancellationToken cancellationToken = default);
    Task<IActionResult> LinkPartnerCoupon(LinkPartnerCouponSignature request, CancellationToken cancellationToken = default);
    Task<IActionResult> GetReferralCoupon(CancellationToken cancellationToken = default);
    Task<IActionResult> DeleteAccount(DeleteAccountSignature request, CancellationToken cancellationToken = default);
}
