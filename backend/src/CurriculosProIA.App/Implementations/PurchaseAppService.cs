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
using System.Globalization;
using System.Text;
using System.Text.Json;

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
            body.Price == null)
        {
            return BadRequest(new
            {
                success = false,
                error = "Dados do plano são obrigatórios"
            });
        }

        if (body.PlanId == "english")
        {
            if (string.IsNullOrWhiteSpace(body.AnalysisId))
            {
                return BadRequest(new
                {
                    success = false,
                    error = "Análise obrigatória",
                    message = "Informe analysisId para comprar o currículo em inglês."
                });
            }

            if (!await _data.UserOwnsAnalysisAsync(userId, body.AnalysisId, cancellationToken))
            {
                return StatusCode(403, new { success = false, error = "Análise não encontrada" });
            }

            if (await _data.HasEnglishPaidAsync(body.AnalysisId, cancellationToken))
            {
                return BadRequest(new
                {
                    success = false,
                    error = "Já adquirido",
                    message = "Esta análise já possui o currículo em inglês."
                });
            }
        }
        else if (body.CreditsAmount == null)
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

        if (body.PlanId == "english")
        {
            await _data.CreatePurchaseAsync(
                userId,
                "english",
                body.PlanName,
                0,
                finalPrice,
                paymentMethod: "mock",
                paymentId: $"mock_english_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{userId}",
                serviceType: "curriculo_ingles",
                analysisId: body.AnalysisId,
                couponId: couponId,
                couponName: couponName,
                discountPercent: discountPercent,
                originalPrice: originalPrice,
                cancellationToken: cancellationToken);

            await _data.GrantEnglishPaidAsync(body.AnalysisId!, cancellationToken);

            return Ok(new
            {
                success = true,
                message = "Currículo em inglês adquirido para esta análise.",
                analysisId = body.AnalysisId
            });
        }

        var purchase = await _data.CreatePurchaseAsync(
            userId,
            body.PlanId,
            body.PlanName,
            body.CreditsAmount!.Value,
            finalPrice,
            couponId: couponId,
            couponName: couponName,
            discountPercent: discountPercent,
            originalPrice: originalPrice,
            cancellationToken: cancellationToken);

        if (body.IncludeEnglish == true)
        {
            var englishPrice = body.EnglishPrice ?? 5.90m;
            await _data.CreatePurchaseAsync(
                userId,
                "english",
                "Currículo em Inglês (bundle)",
                0,
                englishPrice,
                paymentMethod: "mock",
                paymentId: $"mock_english_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{userId}",
                parentPurchaseId: purchase.Id,
                serviceType: "curriculo_ingles",
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

    public async Task<IActionResult> ExportHistory(
        string format = "json",
        int limit = 500,
        CancellationToken cancellationToken = default)
    {
        var userId = JwtAuthHelper.TryGetUserId(_http.HttpContext!.Request.Headers, _configuration);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new { success = false, error = "Token não fornecido" });
        }

        limit = Math.Clamp(limit, 1, 2000);
        var normalizedFormat = (format ?? "json").Trim().ToLowerInvariant();

        var profile = await _data.GetUserProfileAsync(userId, cancellationToken);
        if (profile == null)
        {
            return NotFound(new { success = false, error = "Usuário não encontrado" });
        }

        var purchases = await _data.GetUserPurchasesAsync(userId, limit, cancellationToken);
        var creditUsage = await _data.GetUserCreditUsageAsync(userId, limit, cancellationToken);

        var exportPayload = new
        {
            exportedAt = DateTimeOffset.UtcNow,
            profile = new
            {
                id = profile.Id,
                name = profile.Name,
                email = profile.Email,
                cpf = profile.Cpf,
                dateOfBirth = profile.DateOfBirth,
                city = profile.City,
                country = profile.Country,
                credits = profile.Credits,
                createdAt = profile.CreatedAt
            },
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
                    paymentId = p.PaymentId,
                    serviceType = p.ServiceType,
                    parentPurchaseId = p.ParentPurchaseId,
                    createdAt = p.CreatedAt,
                    credits = new
                    {
                        total = creditsInfo.Total,
                        used = creditsInfo.Used,
                        available = creditsInfo.Available,
                        items = creditsInfo.Credits
                    }
                };
            }),
            creditUsage = creditUsage.Select(u => new
            {
                id = u.Id,
                purchaseId = u.PurchaseId,
                used = u.Used,
                usedAt = u.UsedAt,
                actionType = u.ActionType,
                resumeFileName = u.ResumeFileName,
                createdAt = u.CreatedAt
            })
        };

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);

        if (normalizedFormat == "csv")
        {
            var csv = BuildPurchasesCsv(purchases);
            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
            return File(bytes, "text/csv; charset=utf-8", $"compras-curriculoproia-{timestamp}.csv");
        }

        var json = JsonSerializer.Serialize(exportPayload, new JsonSerializerOptions { WriteIndented = true });
        var jsonBytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(json)).ToArray();
        return File(jsonBytes, "application/json; charset=utf-8", $"dados-compras-curriculoproia-{timestamp}.json");
    }

    private static string BuildPurchasesCsv(IReadOnlyList<PurchaseWithCredits> purchases)
    {
        var sb = new StringBuilder();
        sb.AppendLine("id;plano;plano_id;creditos;preco_brl;status;metodo_pagamento;id_pagamento;tipo_servico;data_compra;creditos_usados;creditos_disponiveis");

        foreach (var p in purchases)
        {
            var creditsInfo = p.CreditsInfo ?? new PurchaseCreditsInfo();
            sb.Append(EscapeCsvField(p.Id)).Append(';');
            sb.Append(EscapeCsvField(p.PlanName)).Append(';');
            sb.Append(EscapeCsvField(p.PlanId)).Append(';');
            sb.Append(EscapeCsvField(p.CreditsAmount.ToString(CultureInfo.InvariantCulture))).Append(';');
            sb.Append(EscapeCsvField((p.Price ?? 0).ToString(CultureInfo.InvariantCulture))).Append(';');
            sb.Append(EscapeCsvField(p.Status)).Append(';');
            sb.Append(EscapeCsvField(p.PaymentMethod)).Append(';');
            sb.Append(EscapeCsvField(p.PaymentId)).Append(';');
            sb.Append(EscapeCsvField(p.ServiceType)).Append(';');
            sb.Append(EscapeCsvField(p.CreatedAt?.ToString("o", CultureInfo.InvariantCulture))).Append(';');
            sb.Append(EscapeCsvField(creditsInfo.Used.ToString(CultureInfo.InvariantCulture))).Append(';');
            sb.AppendLine(EscapeCsvField(creditsInfo.Available.ToString(CultureInfo.InvariantCulture)));
        }

        return sb.ToString();
    }

    private static string EscapeCsvField(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var needsQuotes = value.Contains(';') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
        var escaped = value.Replace("\"", "\"\"", StringComparison.Ordinal);
        return needsQuotes ? $"\"{escaped}\"" : escaped;
    }

}
