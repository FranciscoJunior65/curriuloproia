using CurriculosProIA.Domain.Dtos;

namespace CurriculosProIA.Repository.Interfaces;

public interface ICouponAdminRepository
{
    Task<List<PartnerDto>> ListPartnersAsync(CancellationToken cancellationToken = default);
    Task<PartnerDto> CreatePartnerAsync(
        string nome,
        string cpf,
        string? descricao,
        string? email,
        CancellationToken cancellationToken = default);
    Task<List<AdminCouponDto>> ListCouponsAdminAsync(CancellationToken cancellationToken = default);
    Task<AdminCouponDto> CreateCouponAsync(
        string nome,
        decimal porcentagemDesconto,
        string? parceiroId,
        decimal? porcentagemParceiro,
        CancellationToken cancellationToken = default);
    Task<AdminCouponDto?> UpdateCouponAsync(
        string couponId,
        decimal? porcentagemDesconto,
        string? parceiroId,
        decimal? porcentagemParceiro,
        bool? ativo,
        bool clearParceiro,
        CancellationToken cancellationToken = default);
    Task<CouponMetricsSummaryDto> GetCouponMetricsAsync(CancellationToken cancellationToken = default);
}
