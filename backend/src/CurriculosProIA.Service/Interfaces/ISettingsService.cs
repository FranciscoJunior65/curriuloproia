namespace CurriculosProIA.Service.Interfaces;

public interface ISettingsService
{
    Task<string> GetPaymentProviderAsync(CancellationToken cancellationToken = default);
    Task<string> SetPaymentProviderAsync(string provider, CancellationToken cancellationToken = default);
    IReadOnlyList<string> GetValidPaymentProviders();
    void ClearPaymentProviderCache();
}
