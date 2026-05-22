using System.Text.Json;

namespace CurriculosProIA.Domain.Helpers;

/// <summary>
/// JsonElement vindos do Postgrest/Supabase não podem ser re-serializados pelo ASP.NET
/// após o documento original ser descartado — é preciso clonar via round-trip.
/// </summary>
public static class JsonElementCloneHelper
{
    public static JsonElement? CloneOrNull(JsonElement? element)
    {
        if (element is not { } el)
        {
            return null;
        }

        if (el.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<JsonElement>(el.GetRawText());
        }
        catch
        {
            return null;
        }
    }
}
