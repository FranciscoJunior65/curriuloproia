namespace CurriculosProIA.Service.Interfaces;

public class PurchaseConfirmationDetails
{
    public string PlanName { get; set; } = "-";
    public int? CreditsAmount { get; set; }
    public int? Analyses { get; set; }
    public object? Price { get; set; }
    public string? CustomerName { get; set; }
    public string? ExtraInfo { get; set; }
    public string? CouponName { get; set; }
    public object? DiscountPercent { get; set; }
    public object? OriginalPrice { get; set; }
}

public interface IEmailService
{
    string GenerateVerificationCode();
    Task SendVerificationEmailAsync(string email, string code, string name = "", CancellationToken cancellationToken = default);
    Task SendWelcomeEmailAsync(string email, string name = "", CancellationToken cancellationToken = default);
    Task SendLoginNotificationEmailAsync(string email, string name = "", CancellationToken cancellationToken = default);
    Task SendVerificationLinkEmailAsync(string email, string token, string name = "", CancellationToken cancellationToken = default);
    Task SendPasswordResetEmailAsync(string email, string token, string name = "", CancellationToken cancellationToken = default);
    Task SendPasswordChangeNotificationEmailAsync(string email, string name = "", CancellationToken cancellationToken = default);
    Task SendLoginCodeEmailAsync(string email, string code, string name = "", CancellationToken cancellationToken = default);
    Task SendPurchaseConfirmationEmailAsync(string clientEmail, PurchaseConfirmationDetails details, CancellationToken cancellationToken = default);
}
