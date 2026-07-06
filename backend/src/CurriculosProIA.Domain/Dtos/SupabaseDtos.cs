using CurriculosProIA.Domain.Entities;

namespace CurriculosProIA.Domain.Dtos;

public class CouponValidationResult
{
    public bool Valid { get; set; }
    public Coupon? Coupon { get; set; }
    public string? Message { get; set; }
}

public class PurchaseCreditsInfo
{
    public int Total { get; set; }
    public int Used { get; set; }
    public int Available { get; set; }
    public List<Credit> Credits { get; set; } = new();
}

public class PurchaseWithCredits : Purchase
{
    public PurchaseCreditsInfo? CreditsInfo { get; set; }
}

public class SalesStatsDto
{
    public int TotalPurchases { get; set; }
    public double TotalRevenue { get; set; }
    public double ApprovedRevenue { get; set; }
    public double PendingRevenue { get; set; }
    public int TotalCreditsSold { get; set; }
    public int CompletedPurchases { get; set; }
    public int PendingPurchases { get; set; }
    public int CancelledPurchases { get; set; }
    public int UniqueBuyers { get; set; }
}

public class PurchaseBuyerDto
{
    public string Id { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Name { get; set; }
    public int Credits { get; set; }
    public int PurchasesCount { get; set; }
    public DateTimeOffset? LastPurchaseAt { get; set; }
}

public class CreditUsageResultDto
{
    public bool Success { get; set; }
    public int CreditsUsed { get; set; }
    public string? Id { get; set; }
}

public class AddCreditsResultDto
{
    public bool Success { get; set; }
}

public class DeductCreditsResultDto
{
    public bool Success { get; set; }
}

public class AdminDashboardStatsDto
{
    public int TotalUsers { get; set; }
    public int TotalCredits { get; set; }
    public int CreditsUsed { get; set; }
    public int CreditsAvailable { get; set; }
    public int AnalysesPerformed { get; set; }
    public decimal EstimatedRevenue { get; set; }
    public int ActiveUsers { get; set; }
}
