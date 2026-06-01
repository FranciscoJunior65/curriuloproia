namespace CurriculosProIA.Repository.Interfaces;

/// <summary>Aggregates all repository interfaces for application-layer orchestration.</summary>
public interface IAppDataStore : IUserProfileRepository, IPurchaseRepository, ICreditRepository,
    ICouponRepository, ICouponAdminRepository, IPartnerReferralRepository, IAppSettingsRepository,
    IAnalysisRepository, ICurriculoRepository, IInterviewRepository, IJobSiteRepository
{
}
