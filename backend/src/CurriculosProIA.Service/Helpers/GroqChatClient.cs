using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace CurriculosProIA.Service.Helpers;

public static class GroqChatClient
{
    private static readonly Uri ChatCompletionsUri = new("https://api.groq.com/openai/v1/chat/completions");

    public static object BuildRequest(string model, string prompt, double temperature, int maxOutputTokens) =>
        new
        {
            model,
            messages = new[]
            {
                new { role = "user", content = prompt }
            },
            temperature,
            max_tokens = maxOutputTokens
        };

    public static string ExtractText(string payload)
    {
        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;

        if (root.TryGetProperty("choices", out var choices) &&
            choices.ValueKind == JsonValueKind.Array &&
            choices.GetArrayLength() > 0)
        {
            var first = choices[0];
            if (first.TryGetProperty("message", out var message) &&
                message.TryGetProperty("content", out var content))
            {
                return content.GetString() ?? string.Empty;
            }
        }

        if (root.TryGetProperty("error", out var error) &&
            error.TryGetProperty("message", out var errorMessage))
        {
            throw new InvalidOperationException($"Groq API error: {errorMessage.GetString()}");
        }

        throw new InvalidOperationException("Groq API error: resposta sem conteúdo de texto.");
    }

    public static async Task<string> SendAsync(
        HttpClient client,
        string apiKey,
        string model,
        string prompt,
        double temperature,
        int maxOutputTokens,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, ChatCompletionsUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = JsonContent.Create(BuildRequest(model, prompt, temperature, maxOutputTokens));

        using var response = await client.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return ExtractText(payload);
        }

        throw new InvalidOperationException($"Groq API error: {response.StatusCode} - {payload}");
    }

    public static bool IsTransientStatus(System.Net.HttpStatusCode statusCode) =>
        statusCode == System.Net.HttpStatusCode.ServiceUnavailable
        || statusCode == System.Net.HttpStatusCode.TooManyRequests
        || statusCode == System.Net.HttpStatusCode.GatewayTimeout
        || statusCode == System.Net.HttpStatusCode.RequestTimeout
        || statusCode == System.Net.HttpStatusCode.BadGateway;

    public static bool IsTransientError(Exception ex)
    {
        var msg = ex.Message ?? string.Empty;
        return msg.Contains("429", StringComparison.Ordinal)
            || msg.Contains("503", StringComparison.Ordinal)
            || msg.Contains("502", StringComparison.Ordinal)
            || msg.Contains("504", StringComparison.Ordinal)
            || msg.Contains("rate limit", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("UNAVAILABLE", StringComparison.OrdinalIgnoreCase);
    }
}
