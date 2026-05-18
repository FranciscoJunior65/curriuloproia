using CurriculosProIA.Domain.Dtos;
using CurriculosProIA.Repository.Persistence;

namespace CurriculosProIA.Repository.Interfaces;

public interface ICouponRepository
{
    string NormalizeCpf(string? cpf);
    Task<bool> CouponAlreadyUsedByCpfAsync(string couponId, string cpf, CancellationToken cancellationToken = default);
    Task RegisterCouponUseAsync(string couponId, string cpf, CancellationToken cancellationToken = default);
    Task<CupomRow?> GetCouponByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<CouponValidationResult> ValidateCouponAsync(string code, string? cpf = null, CancellationToken cancellationToken = default);
}
