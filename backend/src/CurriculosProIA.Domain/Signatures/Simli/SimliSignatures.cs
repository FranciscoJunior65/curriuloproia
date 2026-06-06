namespace CurriculosProIA.Domain.Signatures.Simli;

public class CreateSimliSessionSignature
{
    public string? FaceId { get; set; }
    public string? PersonaInitials { get; set; }
}

public class SimliSpeechSignature
{
    public string? Text { get; set; }
    public string? Voice { get; set; }
}
