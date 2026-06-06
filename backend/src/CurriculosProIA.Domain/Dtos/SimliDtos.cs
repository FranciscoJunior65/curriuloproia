namespace CurriculosProIA.Domain.Dtos;

public class SimliConfigDto
{
    public bool Enabled { get; set; }
    public string TransportMode { get; set; } = "livekit";
    public string DefaultFaceId { get; set; } = string.Empty;
    public Dictionary<string, string> FaceIdsByPersona { get; set; } = new();
}

public class SimliSessionRequestDto
{
    public string? FaceId { get; set; }
    public string? PersonaInitials { get; set; }
}

public class SimliSessionResponseDto
{
    public string SessionToken { get; set; } = string.Empty;
    public string FaceId { get; set; } = string.Empty;
}

public class SimliSpeechRequestDto
{
    public string Text { get; set; } = string.Empty;
    public string? Voice { get; set; }
}
