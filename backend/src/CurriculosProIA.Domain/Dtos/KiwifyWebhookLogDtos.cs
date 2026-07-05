namespace CurriculosProIA.Domain.Dtos;

public class KiwifyWebhookHandleResult
{
    public PaymentVerificationResult? Verification { get; set; }
    public string? FailureStage { get; set; }
    public string? FailureMessage { get; set; }
    public string? FailureDetails { get; set; }
}

public class CreateKiwifyWebhookLogRequest
{
    public string? PayloadRecebido { get; set; }
    public string? PayloadParseado { get; set; }
    public string? OrderId { get; set; }
    public string? OrderRef { get; set; }
    public string? EventType { get; set; }
    public string? PaymentStatus { get; set; }
    public bool Processed { get; set; }
    public bool AlreadyFulfilled { get; set; }
    public int? Credits { get; set; }
    public string? UserId { get; set; }
    public int HttpStatus { get; set; } = 200;
    public string? ApiVersion { get; set; }
    public string? Message { get; set; }
    public string? RespostaJson { get; set; }
    public string? Erro { get; set; }
    public string? FailureStage { get; set; }
    public string? ProcessingDetails { get; set; }
}

public class KiwifyWebhookLogDto
{
    public string Id { get; set; } = string.Empty;
    public string? PayloadRecebido { get; set; }
    public string? PayloadParseado { get; set; }
    public string? OrderId { get; set; }
    public string? OrderRef { get; set; }
    public string? EventType { get; set; }
    public string? PaymentStatus { get; set; }
    public bool Processed { get; set; }
    public bool AlreadyFulfilled { get; set; }
    public int? Credits { get; set; }
    public string? UserId { get; set; }
    public int HttpStatus { get; set; }
    public string? ApiVersion { get; set; }
    public string? Message { get; set; }
    public string? RespostaJson { get; set; }
    public string? Erro { get; set; }
    public string? FailureStage { get; set; }
    public string? ProcessingDetails { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
}
