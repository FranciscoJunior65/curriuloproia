using CurriculosProIA.Domain.Dtos;
using CurriculosProIA.Domain.Entities;

namespace CurriculosProIA.Repository.Interfaces;

public interface ICreditRepository
{
    Task<int> GetAvailableCreditsAsync(string userId, CancellationToken cancellationToken = default);
    Task<bool> UserHasCreditsAsync(string userId, int amount = 1, CancellationToken cancellationToken = default);
    Task<int> GetUserCreditsAsync(string userId, CancellationToken cancellationToken = default);
    Task<List<Credit>> GetCreditsByPurchaseAsync(string purchaseId, CancellationToken cancellationToken = default);
    Task<CreditUsageResultDto> RecordCreditUsageAsync(
        string userId, string actionType, int creditsUsed = 1,
        string? resumeFileName = null, string? siteId = null,
        CancellationToken cancellationToken = default);
    Task<List<Credit>> GetUserCreditUsageAsync(string userId, int limit = 50, CancellationToken cancellationToken = default);
}
