namespace CurriculosProIA.Api.Infrastructure;

public static class GeminiConfigHelper
{
    private static readonly string[] PlaceholderKeys =
    [
        "sua-chave-gemini-aqui",
        "sua-chave-aqui",
        "your-gemini-api-key",
        "your-api-key-here"
    ];

    public static bool IsValidApiKey(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return false;
        }

        var trimmed = apiKey.Trim();
        if (trimmed.Length < 30)
        {
            return false;
        }

        foreach (var placeholder in PlaceholderKeys)
        {
            if (trimmed.Equals(placeholder, StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains(placeholder, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    public static string? GetInvalidKeyReason(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return "GEMINI_API_KEY não está definida no .env";
        }

        var trimmed = apiKey.Trim();
        foreach (var placeholder in PlaceholderKeys)
        {
            if (trimmed.Equals(placeholder, StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains(placeholder, StringComparison.OrdinalIgnoreCase))
            {
                return "GEMINI_API_KEY ainda está com o valor de exemplo do ENV_EXAMPLE.env. Substitua por uma chave real do Google AI Studio.";
            }
        }

        if (trimmed.Length < 30)
        {
            return "GEMINI_API_KEY parece inválida (muito curta). Chaves do Google AI Studio costumam começar com AIza e ter ~39 caracteres.";
        }

        return null;
    }
}
