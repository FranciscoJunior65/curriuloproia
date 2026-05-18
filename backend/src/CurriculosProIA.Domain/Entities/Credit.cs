namespace CurriculosProIA.Domain.Entities;

public class Credit
{
    public string Id { get; set; } = string.Empty;
    public string? PurchaseId { get; set; }
    public string? UserId { get; set; }
    public bool Used { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public string? ActionType { get; set; }
    public string? ResumeFileName { get; set; }
    public string? SiteId { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
}
