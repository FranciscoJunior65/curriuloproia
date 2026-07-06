using Microsoft.Extensions.Configuration;

namespace CurriculosProIA.Service.Helpers;

public static class AiProviderOptions
{
    public const string ProviderGemini = "gemini";
    public const string ProviderGroq = "groq";

    public static string GetPrimaryProvider(IConfiguration configuration) =>
        (configuration["AI_PROVIDER"] ?? ProviderGemini).Trim().ToLowerInvariant();

    public static bool IsGroqFallbackEnabled(IConfiguration configuration)
    {
        var flag = configuration["AI_GROQ_FALLBACK"];
        if (string.Equals(flag, "false", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(flag, "0", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return GroqApiKeyValidator.IsConfigured(configuration["GROQ_API_KEY"]);
    }

    public static IReadOnlyList<string> GetGroqModelChain(IConfiguration configuration)
    {
        const string defaultModel = "llama-3.3-70b-versatile";
        string[] defaultFallbacks = ["llama-3.3-70b-versatile", "llama-3.1-8b-instant"];

        var primary = configuration["GROQ_MODEL"]?.Trim();
        if (string.IsNullOrWhiteSpace(primary))
        {
            primary = defaultModel;
        }

        var configuredFallbacks = configuration["GROQ_FALLBACK_MODELS"];
        var fallbacks = string.IsNullOrWhiteSpace(configuredFallbacks)
            ? defaultFallbacks
            : configuredFallbacks.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return new[] { primary }
            .Concat(fallbacks)
            .Where(model => !string.IsNullOrWhiteSpace(model))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
