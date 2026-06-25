namespace CurriculosProIA.Domain.Signatures.Analyze;

public class CaktoCardTokenSignature
{
    public string? HolderName { get; set; }
    public string? CardNumber { get; set; }
    public string? ExpMonth { get; set; }
    public string? ExpYear { get; set; }
    public string? Cvv { get; set; }
}
