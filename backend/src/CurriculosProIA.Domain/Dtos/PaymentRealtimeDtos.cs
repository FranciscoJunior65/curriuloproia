namespace CurriculosProIA.Domain.Dtos;

public class PaymentConfirmedNotification
{
    public string UserId { get; set; } = string.Empty;
    public int Credits { get; set; }
    public string? OrderId { get; set; }
    public string? PlanId { get; set; }
    public string Provider { get; set; } = "kiwify";
    public bool AlreadyFulfilled { get; set; }
}
