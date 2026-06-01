using CurriculosProIA.Domain.Entities;
using System.Text.Json.Serialization;



namespace CurriculosProIA.Domain.Dtos;

public class ProfitMarginResult
{
    public decimal TotalCost { get; set; }
    public decimal Profit { get; set; }
    public decimal Margin { get; set; }
}

public class ResumeAnalysisResult
{
    [JsonPropertyName("pontosFortes")]
    public List<string> PontosFortes { get; set; } = new();

    [JsonPropertyName("pontosMelhorar")]
    public List<string> PontosMelhorar { get; set; } = new();

    [JsonPropertyName("experiencia")]
    public string Experiencia { get; set; } = string.Empty;

    [JsonPropertyName("formacao")]
    public string Formacao { get; set; } = string.Empty;

    [JsonPropertyName("habilidades")]
    public List<string> Habilidades { get; set; } = new();

    [JsonPropertyName("recomendacoes")]
    public List<string> Recomendacoes { get; set; } = new();

    [JsonPropertyName("score")]
    public int Score { get; set; }
}

public class CheckoutContext
{
    public bool FreeCheckout { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string PlanId { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public int Analyses { get; set; }
    public decimal AmountBRL { get; set; }
    public long AmountInCents { get; set; }
    public PricingPlan Plan { get; set; } = null!;
    public Dictionary<string, string> Metadata { get; set; } = new();
    public CheckoutCouponInfo? CouponInfo { get; set; }
    public string? CpfNormalized { get; set; }
}

public class CheckoutCouponInfo
{
    public string CouponId { get; set; } = string.Empty;
    public string CouponName { get; set; } = string.Empty;
    public decimal DiscountPercent { get; set; }
    public decimal OriginalPrice { get; set; }
}

public class CheckoutSessionResult
{
    public bool FreeCheckout { get; set; }
    public string? SessionId { get; set; }
    public string? Url { get; set; }
    public string? PreferenceId { get; set; }
    public string? UserId { get; set; }
    public string? PlanId { get; set; }
    public string? PlanName { get; set; }
    public int Analyses { get; set; }
    public string? CouponId { get; set; }
    public string? CouponName { get; set; }
    public decimal? DiscountPercent { get; set; }
    public decimal? OriginalPrice { get; set; }
    public string? CpfNormalized { get; set; }
}

public class PaymentVerificationResult
{
    public bool Paid { get; set; }
    public string? PaymentStatus { get; set; }
    public string? StatusDetail { get; set; }
    public PaymentUserSummary? User { get; set; }
    public bool AlreadyFulfilled { get; set; }
}

public class PaymentUserSummary
{
    public string Id { get; set; } = string.Empty;
    public int Credits { get; set; }
    public string? Plan { get; set; }
}

public class FulfillOrderRequest
{
    public string UserId { get; set; } = string.Empty;
    public string PlanId { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public int Analyses { get; set; }
    public decimal Price { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string PaymentId { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string? CouponId { get; set; }
    public string? CouponName { get; set; }
    public decimal? DiscountPercent { get; set; }
    public decimal? OriginalPrice { get; set; }
    public string? CpfNormalized { get; set; }
    public string ExtraInfo { get; set; } = string.Empty;
    public bool IncludeEnglish { get; set; }
    public decimal EnglishPriceBRL { get; set; }
    public string? AnalysisId { get; set; }
}

public class FulfillOrderResult
{
    public bool AlreadyFulfilled { get; set; }
    public PaymentUserSummary User { get; set; } = new();
}

public class ProviderCheckoutResult
{
    public string Provider { get; set; } = string.Empty;
    public bool FreeCheckout { get; set; }
    public string? SessionId { get; set; }
    public string? Url { get; set; }
    public string? PreferenceId { get; set; }
    public string? UserId { get; set; }
    public string? PlanId { get; set; }
    public string? PlanName { get; set; }
    public int Analyses { get; set; }
    public string? CouponId { get; set; }
    public string? CouponName { get; set; }
    public decimal? DiscountPercent { get; set; }
    public decimal? OriginalPrice { get; set; }
    public string? CpfNormalized { get; set; }
}

public class PaymentProviderTestResult
{
    public bool Connected { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public object? Details { get; set; }
}
