namespace CurriculosProIA.Domain.Dtos;

public class PartnerDto
{
    public string Id { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string? Cpf { get; set; }
    public string? Descricao { get; set; }
    public string? Email { get; set; }
    public bool Ativo { get; set; } = true;
    public DateTimeOffset? CriadoEm { get; set; }
}

public class AdminCouponDto
{
    public string Id { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public decimal PorcentagemDesconto { get; set; }
    public bool Ativo { get; set; } = true;
    public string? ParceiroId { get; set; }
    public string? ParceiroNome { get; set; }
    public decimal? PorcentagemParceiro { get; set; }
    public int TotalCompras { get; set; }
    public int TotalUsosCpf { get; set; }
    public int TotalCadastrosViaLink { get; set; }
    public decimal ReceitaTotal { get; set; }
    public decimal TotalParceiro { get; set; }
    public string? LinkParceiro { get; set; }
    public DateTimeOffset? CriadoEm { get; set; }
}

public class PartnerReferralDto
{
    public string CouponId { get; set; } = string.Empty;
    public string CouponCode { get; set; } = string.Empty;
    public decimal DiscountPercent { get; set; }
    public string? PartnerId { get; set; }
    public string? PartnerName { get; set; }
    public DateTimeOffset? LinkedAt { get; set; }
}

public class PartnerReferralAdminDto
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? UserEmail { get; set; }
    public string? UserCpf { get; set; }
    public DateTimeOffset? UserCreatedAt { get; set; }
    public string CouponId { get; set; } = string.Empty;
    public string CouponCode { get; set; } = string.Empty;
    public decimal DiscountPercent { get; set; }
    public string? PartnerId { get; set; }
    public string? PartnerName { get; set; }
    public string? PartnerLink { get; set; }
    public DateTimeOffset? LinkedAt { get; set; }
}

public class CouponMetricItemDto
{
    public string CouponId { get; set; } = string.Empty;
    public string CouponName { get; set; } = string.Empty;
    public decimal DiscountPercent { get; set; }
    public bool Ativo { get; set; }
    public string? ParceiroId { get; set; }
    public string? ParceiroNome { get; set; }
    public decimal? ParceiroPercent { get; set; }
    public int PurchasesCount { get; set; }
    public int UniqueCpfUses { get; set; }
    public decimal RevenueTotal { get; set; }
    public decimal PartnerTotal { get; set; }
}

public class PartnerMetricItemDto
{
    public string ParceiroId { get; set; } = string.Empty;
    public string ParceiroNome { get; set; } = string.Empty;
    public int CouponsCount { get; set; }
    public int PurchasesCount { get; set; }
    public decimal RevenueTotal { get; set; }
    public decimal PartnerTotal { get; set; }
}

public class CouponMetricsSummaryDto
{
    public List<CouponMetricItemDto> ByCoupon { get; set; } = new();
    public List<PartnerMetricItemDto> ByPartner { get; set; } = new();
    public int TotalPurchasesWithCoupon { get; set; }
    public decimal TotalRevenueWithCoupon { get; set; }
    public decimal TotalPartnerPayout { get; set; }
}
