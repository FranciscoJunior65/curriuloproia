using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
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


using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CurriculosProIA.App.Implementations;
using CurriculosProIA.App;
using CurriculosProIA.App.Interfaces;

public class PurchaseAppService : AppControllerBase, IPurchaseAppService 
{
    private readonly IHttpContextAccessor _http;
    private readonly IAppDataStore _data;
    private readonly IConfiguration _configuration;

    public PurchaseAppService(IAppDataStore data, IConfiguration configuration,
        IHttpContextAccessor http)
    {
        _http = http;
        _data = data;
        _configuration = configuration;
    }

        public IActionResult Test() =>
        Ok(new
        {
            success = true,
            message = "Rota de compra está funcionando!",
            path = "/api/purchase/test",
            timestamp = DateTime.UtcNow.ToString("o")
        });

        public async Task<IActionResult> CreateMockPurchase(MockPurchaseSignature body, CancellationToken cancellationToken)
    {
        var userId = body.UserId ?? JwtAuthHelper.TryGetUserId(_http.HttpContext!.Request.Headers, _configuration);
        if (string.IsNullOrEmpty(userId))
        {
            return BadRequest(new
            {
                success = false,
                error = "userId é obrigatório",
                message = "Envie userId no body da requisição."
            });
        }

        if (string.IsNullOrEmpty(body.PlanId) || string.IsNullOrEmpty(body.PlanName) ||
            body.CreditsAmount == null || body.Price == null)
        {
            return BadRequest(new
            {
                success = false,
                error = "Dados do plano são obrigatórios"
            });
        }

        var user = await _data.GetUserProfileAsync(userId, cancellationToken);
        if (user == null)
        {
            return NotFound(new { success = false, error = "Usuário não encontrado" });
        }

        var finalPrice = body.Price.Value;
        string? couponId = null;
        string? couponName = null;
        decimal? discountPercent = null;
        decimal? originalPrice = null;

        if (!string.IsNullOrWhiteSpace(body.CouponCode))
        {
            var cpfTrim = body.Cpf?.Trim() ?? string.Empty;
            if (_data.NormalizeCpf(cpfTrim).Length != 11)
            {
                return BadRequest(new
                {
                    success = false,
                    error = "Para usar cupom, informe o CPF (11 dígitos).",
                    code = "CPF_REQUIRED"
                });
            }

            var validation = await _data.ValidateCouponAsync(body.CouponCode.Trim(), cpfTrim, cancellationToken);
            if (!validation.Valid)
            {
                return BadRequest(new
                {
                    success = false,
                    error = validation.Message ?? "Cupom inválido ou já utilizado por este CPF.",
                    code = "COUPON_INVALID"
                });
            }

            if (validation.Coupon != null)
            {
                var pct = (decimal)validation.Coupon.PorcentagemDesconto;
                originalPrice = finalPrice;
                finalPrice = Math.Max(0, finalPrice * (1 - pct / 100));
                couponId = validation.Coupon.Id;
                couponName = validation.Coupon.Nome;
                discountPercent = pct;
            }
        }

        var purchase = await _data.CreatePurchaseAsync(
            userId,
            body.PlanId,
            body.PlanName,
            body.CreditsAmount.Value,
            finalPrice,
            couponId: couponId,
            couponName: couponName,
            discountPercent: discountPercent,
            originalPrice: originalPrice,
            cancellationToken: cancellationToken);

        if (body.IncludeEnglish == true && body.PlanId != "english")
        {
            var englishPrice = body.EnglishPrice ?? 5.90m;
            await _data.CreatePurchaseAsync(
                userId,
                "english",
                "Currículo em Inglês (Venda Casada)",
                0,
                englishPrice,
                paymentMethod: "mock",
                paymentId: $"mock_english_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{userId}",
                cancellationToken: cancellationToken);
        }

        if (!string.IsNullOrEmpty(couponId) && !string.IsNullOrWhiteSpace(body.Cpf))
        {
            await _data.RegisterCouponUseAsync(couponId, body.Cpf, cancellationToken);
        }

        var creditsAvailable = await _data.GetAvailableCreditsAsync(userId, cancellationToken);

        return Ok(new
        {
            success = true,
            message = "Compra realizada com sucesso!",
            purchase = new
            {
                id = purchase.Id,
                planName = purchase.PlanName,
                creditsAmount = purchase.CreditsAmount,
                price = purchase.Price,
                status = purchase.Status,
                createdAt = purchase.CreatedAt
            },
            user = new { id = userId, credits = creditsAvailable },
            creditsAvailable
        });
    }

        public async Task<IActionResult> GetHistory(int limit = 50, CancellationToken cancellationToken = default)
    {
        var userId = JwtAuthHelper.TryGetUserId(_http.HttpContext!.Request.Headers, _configuration);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new { success = false, error = "Token não fornecido" });
        }

        var purchases = await _data.GetUserPurchasesAsync(userId, limit, cancellationToken);
        return Ok(new
        {
            success = true,
            purchases = purchases.Select(p =>
            {
                var creditsInfo = p.CreditsInfo ?? new PurchaseCreditsInfo();
                return new
                {
                    id = p.Id,
                    planId = p.PlanId,
                    planName = p.PlanName,
                    creditsAmount = p.CreditsAmount,
                    price = p.Price ?? 0,
                    currency = p.Currency,
                    status = p.Status,
                    paymentMethod = p.PaymentMethod,
                    createdAt = p.CreatedAt,
                    serviceType = p.ServiceType,
                    parentPurchaseId = p.ParentPurchaseId,
                    creditsInfo = new
                    {
                        total = creditsInfo.Total,
                        used = creditsInfo.Used,
                        available = creditsInfo.Available,
                        credits = creditsInfo.Credits
                    }
                };
            })
        });
    }

        public async Task<IActionResult> GetCreditHistory(int limit = 50, CancellationToken cancellationToken = default)
    {
        var userId = JwtAuthHelper.TryGetUserId(_http.HttpContext!.Request.Headers, _configuration);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new { success = false, error = "Token não fornecido" });
        }

        var usage = await _data.GetUserCreditUsageAsync(userId, limit, cancellationToken);
        return Ok(new
        {
            success = true,
            usage = usage.Select(u => new
            {
                id = u.Id,
                purchaseId = u.PurchaseId,
                used = u.Used,
                usedAt = u.UsedAt,
                actionType = u.ActionType,
                resumeFileName = u.ResumeFileName,
                createdAt = u.CreatedAt
            })
        });
    }

        public async Task<IActionResult> RecordCreditUse(RecordCreditUseSignature body, CancellationToken cancellationToken)
    {
        var userId = JwtAuthHelper.TryGetUserId(_http.HttpContext!.Request.Headers, _configuration);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new { success = false, error = "Token não fornecido" });
        }

        if (string.IsNullOrEmpty(body.ActionType))
        {
            return BadRequest(new { success = false, error = "Tipo de ação é obrigatório" });
        }

        var usage = await _data.RecordCreditUsageAsync(
            userId,
            body.ActionType,
            body.CreditsUsed ?? 1,
            body.ResumeFileName,
            cancellationToken: cancellationToken);

        return Ok(new
        {
            success = true,
            usage = new
            {
                id = usage.Id,
                purchaseId = (string?)null,
                used = usage.CreditsUsed,
                usedAt = (DateTimeOffset?)null,
                actionType = body.ActionType,
                createdAt = (DateTimeOffset?)null
            }
        });
    }

}
