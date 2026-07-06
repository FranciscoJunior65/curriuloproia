namespace CurriculosProIA.Service.Helpers;

public static class GroqApiKeyValidator
{
    private static readonly string[] PlaceholderKeys =
    [
        "sua-chave-groq-aqui",
        "your-groq-api-key",
        "gsk_sua-chave",
        "your-api-key-here"
    ];

    public static bool IsConfigured(string? apiKey) => TryValidate(apiKey, out _);

    public static void EnsureValidOrThrow(string? apiKey)
    {
        if (!TryValidate(apiKey, out var reason))
        {
            throw new InvalidOperationException(reason ?? "Groq não configurado. Configure GROQ_API_KEY no .env");
        }
    }

    public static bool TryValidate(string? apiKey, out string? reason)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            reason = "GROQ_API_KEY não está definida no .env";
            return false;
        }

        var trimmed = apiKey.Trim();
        foreach (var placeholder in PlaceholderKeys)
        {
            if (trimmed.Equals(placeholder, StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains(placeholder, StringComparison.OrdinalIgnoreCase))
            {
                reason = "GROQ_API_KEY ainda é o valor de exemplo. Crie uma chave em https://console.groq.com";
                return false;
            }
        }

        if (trimmed.Length < 20 || !trimmed.StartsWith("gsk_", StringComparison.Ordinal))
        {
            reason = "GROQ_API_KEY inválida. Chaves Groq costumam começar com gsk_.";
            return false;
        }

        reason = null;
        return true;
    }
}
