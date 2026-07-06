using CurriculosProIA.Domain.Dtos;
using CurriculosProIA.Domain.Entities;
using CurriculosProIA.Repository.Persistence;

namespace CurriculosProIA.Repository.Interfaces;

public interface IPurchaseRepository
{
    Purchase? MapPurchaseToEnglish(CompraRow? purchase);
    Task<Purchase> CreatePurchaseAsync(
        string userId, string planId, string planName, int creditsAmount, decimal price,
        string currency = "BRL", string paymentMethod = "mock", string? paymentId = null,
        string? parentPurchaseId = null, string serviceType = "analysis_plan",
        string? couponId = null, string? couponName = null,
        decimal? discountPercent = null, decimal? originalPrice = null,
        string? partnerId = null, decimal? partnerPercent = null, decimal? partnerAmount = null,
        string? analysisId = null,
        CancellationToken cancellationToken = default);
    Task<CompraRow?> FindPendingBundledEnglishPurchaseAsync(
        string parentPurchaseId,
        CancellationToken cancellationToken = default);

    /// <summary>Compras de inglês (bundle) ainda não vinculadas a uma análise.</summary>
    Task<int> GetPendingEnglishCreditsAsync(string userId, CancellationToken cancellationToken = default);
    Task<Purchase?> GetPurchaseByPaymentIdAsync(string paymentId, CancellationToken cancellationToken = default);
    Task<Purchase> CreatePendingPurchaseAsync(
        string userId,
        string planId,
        string planName,
        int creditsAmount,
        decimal price,
        string paymentMethod = "kiwify",
        string? paymentId = null,
        CancellationToken cancellationToken = default);
    Task<List<Purchase>> GetPendingPurchasesAsync(
        string? userId = null,
        int limit = 50,
        CancellationToken cancellationToken = default);
    Task UpdatePurchaseStatusAsync(
        string purchaseId,
        string status,
        string? paymentId = null,
        CancellationToken cancellationToken = default);
    Task MarkPendingPurchasesSubstitutedAsync(
        string userId,
        string planId,
        CancellationToken cancellationToken = default);
    Task<List<PurchaseWithCredits>> GetUserPurchasesAsync(string userId, int limit = 50, CancellationToken cancellationToken = default);
    Task<List<Purchase>> GetAllPurchasesAsync(int limit = 100, int offset = 0, CancellationToken cancellationToken = default);
    Task<List<PurchaseBuyerDto>> GetDistinctPurchaseBuyersAsync(int limit = 300, CancellationToken cancellationToken = default);
    Task<SalesStatsDto> GetSalesStatsAsync(string? startDate = null, string? endDate = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DailyUsageDto>> GetDailyUsageAsync(int days, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MonthlyUsageDto>> GetMonthlyUsageAsync(int months, CancellationToken cancellationToken = default);
}
