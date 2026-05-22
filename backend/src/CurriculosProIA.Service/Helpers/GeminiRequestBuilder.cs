namespace CurriculosProIA.Service.Helpers;

public static class GeminiRequestBuilder
{
    public static object BuildGenerateContentRequest(
        string userText,
        double temperature,
        int maxOutputTokens,
        string model)
    {
        var generationConfig = new Dictionary<string, object>
        {
            ["temperature"] = temperature,
            ["maxOutputTokens"] = Math.Max(maxOutputTokens, 256)
        };

        if (model.Contains("gemini-3", StringComparison.OrdinalIgnoreCase))
        {
            generationConfig["thinkingConfig"] = new Dictionary<string, object>
            {
                ["thinkingLevel"] = "low"
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
