using Microsoft.Extensions.Configuration;

namespace CurriculosProIA.Service.Helpers;

public static class KiwifyConfigHelper
{
    public const string BaseUrl = "https://public-api.kiwify.com";

    public static string? GetApiKey(IConfiguration configuration) =>
        configuration["KIWIFY_API_KEY"]?.Trim()
        ?? configuration["KIWIFY_CLIENT_ID"]?.Trim();

    public static string? GetClientSecret(IConfiguration configuration) =>
        configuration["KIWIFY_CLIENT_SECRET"]?.Trim();

    public static string? GetAccountId(IConfiguration configuration) =>
        configuration["KIWIFY_ACCOUNT_ID"]?.Trim();

    public static string? GetWebhookToken(IConfiguration configuration) =>
        configuration["KIWIFY_WEBHOOK_TOKEN"]?.Trim();

    public static string GetCheckoutEnvKey(string planId, bool includeEnglish)
    {
        if (string.Equals(planId, "english", StringComparison.OrdinalIgnoreCase))
        {
            return "KIWIFY_CHECKOUT_ENGLISH";
        }

        if (includeEnglish)
        {
            return $"KIWIFY_CHECKOUT_{planId.ToUpperInvariant()}_ENGLISH";
        }

        return $"KIWIFY_CHECKOUT_{planId.ToUpperInvariant()}";
    }

    public static string? GetCheckoutCode(IConfiguration configuration, string planId, bool includeEnglish)
    {
        if (string.Equals(planId, "english", StringComparison.OrdinalIgnoreCase))
        {
            return configuration["KIWIFY_CHECKOUT_ENGLISH"]?.Trim();
        }

        if (includeEnglish)
        {
            return configuration[GetCheckoutEnvKey(planId, true)]?.Trim();
        }

        return configuration[GetCheckoutEnvKey(planId, false)]?.Trim();
    }

    public static string? BuildMissingCheckoutMessage(string planId, bool includeEnglish) =>
        $"Link de checkout Kiwify não configurado. Defina {GetCheckoutEnvKey(planId, includeEnglish)} no backend/.env.";

    public static string MaskSecret(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "não definido";
        }

        var v = value.Trim();
        if (v.Length <= 8)
        {
            return "***";
        }

        return $"{v[..4]}...{v[^4..]}";
    }

    public static bool HasApiCredentials(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(GetApiKey(configuration))
        && !string.IsNullOrWhiteSpace(GetClientSecret(configuration))
        && !string.IsNullOrWhiteSpace(GetAccountId(configuration));

    public static bool HasCheckoutForPlan(IConfiguration configuration, string planId, bool includeEnglish) =>
        !string.IsNullOrWhiteSpace(GetCheckoutCode(configuration, planId, includeEnglish));

    public static bool IsConfigured(IConfiguration configuration) =>
        HasApiCredentials(configuration);

    public static string BuildMissingConfigMessage(IConfiguration configuration)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(GetApiKey(configuration)))
        {
            missing.Add("KIWIFY_API_KEY");
        }

        if (string.IsNullOrWhiteSpace(GetClientSecret(configuration)))
        {
            missing.Add("KIWIFY_CLIENT_SECRET");
        }

        if (string.IsNullOrWhiteSpace(GetAccountId(configuration)))
        {
            missing.Add("KIWIFY_ACCOUNT_ID");
        }

        if (missing.Count == 0)
        {
            return string.Empty;
        }

        return
            $"Kiwify incompleto: defina {string.Join(", ", missing)} no backend/.env (Apps → API no painel Kiwify).";
    }
}
