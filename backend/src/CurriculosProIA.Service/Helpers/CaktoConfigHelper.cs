using Microsoft.Extensions.Configuration;

namespace CurriculosProIA.Service.Helpers;

public static class CaktoConfigHelper
{
    public const string BaseUrl = "https://api.cakto.com.br";

    public static string? GetClientId(IConfiguration configuration) =>
        configuration["CAKTO_CLIENT_ID"]?.Trim();

    public static string? GetClientSecret(IConfiguration configuration) =>
        configuration["CAKTO_CLIENT_SECRET"]?.Trim();

    public static string? GetSdkClientId(IConfiguration configuration) =>
        configuration["CAKTO_SDK_CLIENT_ID"]?.Trim()
        ?? configuration["CAKTO_CLIENT_ID"]?.Trim();

    public static string? GetProductId(IConfiguration configuration) =>
        configuration["CAKTO_PRODUCT_ID"]?.Trim();

    public static string? GetOfferId(IConfiguration configuration) =>
        configuration["CAKTO_OFFER_ID"]?.Trim();

    public static string? GetWebhookSecret(IConfiguration configuration) =>
        configuration["CAKTO_WEBHOOK_SECRET"]?.Trim();

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
        !string.IsNullOrWhiteSpace(GetClientId(configuration))
        && !string.IsNullOrWhiteSpace(GetClientSecret(configuration));

    public static bool HasCheckoutCatalog(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(GetProductId(configuration))
        && !string.IsNullOrWhiteSpace(GetOfferId(configuration));

    public static bool IsConfigured(IConfiguration configuration) =>
        HasApiCredentials(configuration) && HasCheckoutCatalog(configuration);

    public static string BuildMissingConfigMessage(IConfiguration configuration)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(GetClientId(configuration)))
        {
            missing.Add("CAKTO_CLIENT_ID");
        }

        if (string.IsNullOrWhiteSpace(GetClientSecret(configuration)))
        {
            missing.Add("CAKTO_CLIENT_SECRET");
        }

        if (string.IsNullOrWhiteSpace(GetProductId(configuration)))
        {
            missing.Add("CAKTO_PRODUCT_ID");
        }

        if (string.IsNullOrWhiteSpace(GetOfferId(configuration)))
        {
            missing.Add("CAKTO_OFFER_ID");
        }

        if (missing.Count == 0)
        {
            return string.Empty;
        }

        var hasOAuth = HasApiCredentials(configuration);
        var hint = hasOAuth
            ? " OAuth OK — falta criar produto + oferta no painel Cakto e colar os IDs no backend/.env (veja COMO_ATIVAR_CAKTO.md)."
            : " Cole as chaves da integração Cakto API no backend/.env.";

        return $"Cakto incompleto: defina {string.Join(", ", missing)} no backend/.env.{hint}";
    }
}
