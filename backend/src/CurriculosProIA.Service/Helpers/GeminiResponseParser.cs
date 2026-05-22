using System.Text;
using System.Text.Json;

namespace CurriculosProIA.Service.Helpers;

public static class GeminiResponseParser
{
    public static string ExtractText(string payload)
    {
        using var doc = JsonDocument.Parse(payload);
        return ExtractText(doc.RootElement, payload);
    }

    public static string ExtractText(JsonElement root, string? rawPayloadForError = null)
    {
        if (!root.TryGetProperty("candidates", out var candidates) ||
            candidates.ValueKind != JsonValueKind.Array ||
            candidates.GetArrayLength() == 0)
        {
            var blockReason = root.TryGetProperty("promptFeedback", out var feedback) &&
                              feedback.TryGetProperty("blockReason", out var reason)
                ? reason.GetString()
                : null;
            throw new InvalidOperationException(
                blockReason != null
                    ? $"Gemini bloqueou a resposta: {blockReason}"
                    : "Resposta do Gemini sem candidates. Aumente maxOutputTokens ou verifique o prompt.");
        }

        var candidate = candidates[0];
        if (!candidate.TryGetProperty("content", out var content) ||
            !content.TryGetProperty("parts", out var parts) ||
            parts.ValueKind != JsonValueKind.Array)
        {
            var finish = candidate.TryGetProperty("finishReason", out var fr) ? fr.GetString() : "unknown";
            throw new InvalidOperationException(
                $"Resposta do Gemini sem content/parts (finishReason: {finish}).");
        }

        var textBuilder = new StringBuilder();
        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("thought", out var thoughtFlag) &&
                thoughtFlag.ValueKind == JsonValueKind.True)
            {
                continue;
            }

            if (!part.TryGetProperty("text", out var textElement))
            {
                continue;
            }

            var piece = textElement.GetString();
            if (string.IsNullOrWhiteSpace(piece))
            {
                continue;
            }

            if (piece.StartsWith("THOUGHT:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            textBuilder.Append(piece);
        }

        var result = textBuilder.ToString().Trim();
        if (!string.IsNullOrEmpty(result))
        {
            return result;
        }

        var finishReason = candidate.TryGetProperty("finishReason", out var finishReasonEl)
            ? finishReasonEl.GetString()
            : null;
        throw new InvalidOperationException(
            $"Resposta do Gemini sem texto utilizável (finishReason: {finishReason ?? "unknown"}). " +
            "Para modelos com thinking, use maxOutputTokens >= 256.");
    }
}
