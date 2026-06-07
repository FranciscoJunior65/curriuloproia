namespace CurriculosProIA.Service.Helpers;

public static class GeminiRequestBuilder
{
    private const int DefaultMinOutputTokens = 256;
    private const int ThinkingModelMinOutputTokens = 2048;

    public static object BuildGenerateContentRequest(
        string userText,
        double temperature,
        int maxOutputTokens,
        string model)
    {
        var isGemini3 = model.Contains("gemini-3", StringComparison.OrdinalIgnoreCase);
        var isGemini25 = model.Contains("gemini-2.5", StringComparison.OrdinalIgnoreCase);
        var usesThinking = isGemini3 || isGemini25;

        var effectiveMaxOutputTokens = usesThinking
            ? Math.Max(maxOutputTokens, ThinkingModelMinOutputTokens)
            : Math.Max(maxOutputTokens, DefaultMinOutputTokens);

        var generationConfig = new Dictionary<string, object>
        {
            ["temperature"] = temperature,
            ["maxOutputTokens"] = effectiveMaxOutputTokens
        };

        if (isGemini3)
        {
            // Thinking consome parte de maxOutputTokens; MINIMAL reduz latência e libera tokens para o texto.
            generationConfig["thinkingConfig"] = new Dictionary<string, object>
            {
                ["thinkingLevel"] = "MINIMAL"
            };
        }
        else if (isGemini25)
        {
            generationConfig["thinkingConfig"] = new Dictionary<string, object>
            {
                ["thinkingBudget"] = 0
            };
        }

        return new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = userText } }
                }
            },
            generationConfig
        };
    }
}
