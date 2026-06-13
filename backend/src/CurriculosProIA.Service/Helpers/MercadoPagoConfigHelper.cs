using Microsoft.Extensions.Configuration;

namespace CurriculosProIA.Service.Helpers;

/// <summary>Resolve credenciais Mercado Pago conforme MERCADOPAGO_MODE (test | production).</summary>
public static class MercadoPagoConfigHelper
{
    public const string ModeTest = "test";
    public const string ModeProduction = "production";

    public static string GetMode(IConfiguration configuration, string? overrideMode = null)
    {
        if (!string.IsNullOrWhiteSpace(overrideMode))
        {
            var normalized = overrideMode.Trim().ToLowerInvariant();
            if (normalized is ModeTest or ModeProduction)
            {
                return normalized;
            }
        }

        var mode = configuration["MERCADOPAGO_MODE"]?.Trim().ToLowerInvariant();
        if (mode is ModeTest or ModeProduction)
        {
            return mode;
        }

        var sandbox = configuration["MERCADOPAGO_SANDBOX"]?.Trim();
        if (string.Equals(sandbox, "true", StringComparison.OrdinalIgnoreCase) || sandbox == "1")
        {
            return ModeTest;
        }

        if (string.Equals(sandbox, "false", StringComparison.OrdinalIgnoreCase) || sandbox == "0")
        {
            return ModeProduction;
        }

        return ModeTest;
    }

    public static bool IsProductionMode(IConfiguration configuration, string? overrideMode = null) =>
        GetMode(configuration, overrideMode) == ModeProduction;

    public static bool IsTestMode(IConfiguration configuration, string? overrideMode = null) =>
        !IsProductionMode(configuration, overrideMode);

    public static string? GetAccessToken(IConfiguration configuration, string? overrideMode = null)
    {
        var direct = configuration["MERCADOPAGO_ACCESS_TOKEN"]?.Trim();
        if (!string.IsNullOrEmpty(direct) &&
            !direct.Contains("seu-access-token", StringComparison.OrdinalIgnoreCase))
        {
            return direct;
        }

        var mode = GetMode(configuration, overrideMode);
        var key = GetAccessTokenEnvKey(mode);
        var token = configuration[key]?.Trim();

        return IsPlaceholderToken(token) ? null : token;
    }

    public static string GetAccessTokenEnvKey(string mode) =>
        mode == ModeProduction
            ? "MERCADOPAGO_ACCESS_TOKEN_PRODUCTION"
            : "MERCADOPAGO_ACCESS_TOKEN_TEST";

    public static bool IsPlaceholderToken(string? token) =>
        string.IsNullOrWhiteSpace(token)
        || token.Contains("seu-access-token", StringComparison.OrdinalIgnoreCase)
        || token.StartsWith("APP_USR-seu", StringComparison.OrdinalIgnoreCase);

    public static string BuildMissingTokenMessage(string mode)
    {
        var key = GetAccessTokenEnvKey(mode);
        return mode == ModeProduction
            ? $"Mercado Pago em modo produção: defina {key} no app.env (pasta do site no servidor). " +
              "Se o painel admin está em «Produção», o token de produção é obrigatório."
            : $"Mercado Pago em modo teste: defina {key} no app.env.";
    }

    public static string? GetPublicKey(IConfiguration configuration, string? overrideMode = null)
    {
        var direct = configuration["MERCADOPAGO_PUBLIC_KEY"]?.Trim();
        if (!string.IsNullOrEmpty(direct))
        {
            return direct;
        }

        var mode = GetMode(configuration, overrideMode);
        var key = mode == ModeProduction
            ? "MERCADOPAGO_PUBLIC_KEY_PRODUCTION"
            : "MERCADOPAGO_PUBLIC_KEY_TEST";

        return configuration[key]?.Trim();
    }

    public static string? GetClientId(IConfiguration configuration)
    {
        var mode = GetMode(configuration);
        var key = mode == ModeProduction
            ? "MERCADOPAGO_CLIENT_ID_PRODUCTION"
            : "MERCADOPAGO_CLIENT_ID_TEST";

        return configuration[key]?.Trim() ?? configuration["MERCADOPAGO_CLIENT_ID"]?.Trim();
    }

    public static string? GetClientSecret(IConfiguration configuration)
    {
        var mode = GetMode(configuration);
        var key = mode == ModeProduction
            ? "MERCADOPAGO_CLIENT_SECRET_PRODUCTION"
            : "MERCADOPAGO_CLIENT_SECRET_TEST";

        return configuration[key]?.Trim() ?? configuration["MERCADOPAGO_CLIENT_SECRET"]?.Trim();
    }

    public static object GetDebugInfo(IConfiguration configuration, string? overrideMode = null)
    {
        var mode = GetMode(configuration, overrideMode);
        var token = GetAccessToken(configuration, overrideMode);

        return new
        {
            mode,
            isProduction = mode == ModeProduction,
            checkoutTarget = mode == ModeProduction ? "init_point" : "sandbox_init_point",
            hasAccessToken = !string.IsNullOrWhiteSpace(token),
            tokenPreview = string.IsNullOrWhiteSpace(token) ? null : MaskToken(token),
            hasPublicKey = !string.IsNullOrWhiteSpace(GetPublicKey(configuration)),
            legacySandboxFlag = configuration["MERCADOPAGO_SANDBOX"]
        };
    }

    public static string MaskToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length <= 12)
        {
            return "***";
        }

        return $"{token[..8]}...{token[^4..]}";
    }

    /// <summary>
    /// Sandbox: cartão de crédito (PIX e boleto excluídos).
    /// Produção: cartão, conta Mercado Pago e PIX.
    /// Sempre exclui boleto e cartões de débito (incl. Caixa virtual).
    /// </summary>
    public static object BuildCheckoutPaymentMethods(bool isProduction)
    {
        var excludedTypes = new List<object>
        {
            new { id = "ticket" },
            new { id = "debit_card" }
        };

        if (!isProduction)
        {
            excludedTypes.Add(new { id = "bank_transfer" });
        }

        return new
        {
            excluded_payment_types = excludedTypes,
            excluded_payment_methods = new[]
            {
                new { id = "bolbradesco" }
            }
        };
    }
}
