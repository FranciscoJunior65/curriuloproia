using CurriculosProIA.Service.Interfaces;
using CurriculosProIA.Repository.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CurriculosProIA.Service.Implementations;

public class SettingsService : ISettingsService
{
    private const string PaymentProviderKey = "payment_provider";
    private static readonly string[] ValidProviders = ["stripe", "mercadopago"];

    private readonly IAppSettingsRepository _settingsRepo;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SettingsService> _logger;
    private string? _memoryCache;

    public SettingsService(
        IAppSettingsRepository settingsRepo,
        IConfiguration configuration,
        ILogger<SettingsService> logger)
    {
        _settingsRepo = settingsRepo;
        _configuration = configuration;
        _logger = logger;
    }

    public IReadOnlyList<string> GetValidPaymentProviders() => ValidProviders;

    public void ClearPaymentProviderCache() => _memoryCache = null;

    public async Task<string> GetPaymentProviderAsync(CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(_memoryCache))
        {
            return _memoryCache;
        }

        try
        {
            var value = await _settingsRepo.GetAppConfigValueAsync(PaymentProviderKey, cancellationToken);
            if (!string.IsNullOrEmpty(value))
            {
                _memoryCache = NormalizeProvider(value);
                return _memoryCache;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tabela app_configuracoes indisponível, usando PAYMENT_PROVIDER do .env");
        }

        _memoryCache = GetEnvDefault();
        return _memoryCache;
    }

    public async Task<string> SetPaymentProviderAsync(string provider, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeProvider(provider);
        _memoryCache = normalized;

        try
        {
            await _settingsRepo.SetAppConfigValueAsync(PaymentProviderKey, normalized, cancellationToken);
        }
        catch (Exception ex)
        {
            _memoryCache = null;
            throw new InvalidOperationException($"Erro ao salvar provedor de pagamento: {ex.Message}", ex);
        }

        return normalized;
    }

    private string GetEnvDefault() =>
        NormalizeProvider(_configuration["PAYMENT_PROVIDER"] ?? "stripe");

    private static string NormalizeProvider(string? value)
    {
        var v = (value ?? string.Empty).Trim().ToLowerInvariant();
        return ValidProviders.Contains(v) ? v : "stripe";
    }
}
