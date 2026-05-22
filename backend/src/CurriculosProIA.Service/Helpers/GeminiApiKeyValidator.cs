namespace CurriculosProIA.Service.Helpers;

public static class GeminiApiKeyValidator
{
    private static readonly string[] PlaceholderKeys =
    [
        "sua-chave-gemini-aqui",
        "sua-chave-aqui",
        "your-gemini-api-key",
        "your-api-key-here"
    ];

    public static void EnsureValidOrThrow(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Gemini não configurado. Configure GEMINI_API_KEY no .env");
        }

        var trimmed = apiKey.Trim();
        foreach (var placeholder in PlaceholderKeys)
        {
            if (trimmed.Equals(placeholder, StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains(placeholder, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "GEMINI_API_KEY ainda é o valor de exemplo. Crie uma chave em https://aistudio.google.com/apikey e atualize backend/.env");
            }
        }

        if (trimmed.Length < 30)
        {
            throw new InvalidOperationException(
                "GEMINI_API_KEY inválida. Use uma chave real do Google AI Studio (geralmente começa com AIza).");
        }
    }
}
