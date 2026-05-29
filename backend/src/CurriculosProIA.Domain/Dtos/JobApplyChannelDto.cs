using System.Text.Json.Serialization;

namespace CurriculosProIA.Domain.Dtos;

public class JobApplyChannelDto
{
    [JsonPropertyName("portal")]
    public string Portal { get; set; } = string.Empty;

    [JsonPropertyName("link")]
    public string? Link { get; set; }
}
