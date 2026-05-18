namespace CurriculosProIA.Repository.Interfaces;

public interface IAppSettingsRepository
{
    Task<string?> GetAppConfigValueAsync(string key, CancellationToken cancellationToken = default);
    Task SetAppConfigValueAsync(string key, string value, CancellationToken cancellationToken = default);
}
