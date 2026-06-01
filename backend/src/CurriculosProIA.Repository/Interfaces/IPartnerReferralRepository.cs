using CurriculosProIA.Domain.Dtos;

namespace CurriculosProIA.Repository.Interfaces;

public interface IPartnerReferralRepository
{
    Task<PartnerReferralDto?> GetPartnerReferralByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<bool> UserHasPartnerReferralAsync(string userId, CancellationToken cancellationToken = default);
    Task RegisterPartnerReferralAsync(string userId, string couponCode, CancellationToken cancellationToken = default);
    Task<List<PartnerReferralAdminDto>> ListPartnerReferralsAsync(CancellationToken cancellationToken = default);
    Task<Dictionary<string, int>> CountReferralsByCouponAsync(CancellationToken cancellationToken = default);
}
