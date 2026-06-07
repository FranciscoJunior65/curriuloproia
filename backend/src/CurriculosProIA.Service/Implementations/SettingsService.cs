using CurriculosProIA.Service.Interfaces;
using CurriculosProIA.Repository.Interfaces;
using CurriculosProIA.Service.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CurriculosProIA.Service.Implementations;

public class SettingsService : ISettingsService
{
    private const string PaymentProviderKey = "payment_provider";
    private const string MercadoPagoModeKey = "mercadopago_mode";
    private static readonly string[] ValidProviders = ["stripe", "mercadopago"];
    private static readonly string[] ValidMercadoPagoModes =
        [MercadoPagoConfigHelper.ModeTest, MercadoPagoConfigHelper.ModeProduction];

    private readonly IAppSettingsRepository _settingsRepo;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SettingsService> _logger;
    private string? _memoryCache;
    private string? _mpModeCache;

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

    public IReadOnlyList<string> GetValidMercadoPagoModes() => ValidMercadoPagoModes;

    public void ClearMercadoPagoModeCache() => _mpModeCache = null;

    public async Task<string> GetMercadoPagoModeAsync(CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(_mpModeCache))
        {
            return _mpModeCache;
        }

        try
        {
            var value = await _settingsRepo.GetAppConfigValueAsync(MercadoPagoModeKey, cancellationToken);
            if (!string.IsNullOrEmpty(value))
            {
                _mpModeCache = NormalizeMercadoPagoMode(value);
                return _mpModeCache;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tabela app_configuracoes indisponível, usando MERCADOPAGO_MODE do .env");
        }

        _mpModeCache = MercadoPagoConfigHelper.GetMode(_configuration);
        return _mpModeCache;
    }

    public async Task<string> SetMercadoPagoModeAsync(string mode, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeMercadoPagoMode(mode);
        _mpModeCache = normalized;

        try
        {
            await _settingsRepo.SetAppConfigValueAsync(MercadoPagoModeKey, normalized, cancellationToken);
        }
        catch (Exception ex)
        {
            _mpModeCache = null;
            throw new InvalidOperationException($"Erro ao salvar ambiente Mercado Pago: {ex.Message}", ex);
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

    private static string NormalizeMercadoPagoMode(string? value)
    {
        var v = (value ?? string.Empty).Trim().ToLowerInvariant();
        return ValidMercadoPagoModes.Contains(v) ? v : MercadoPagoConfigHelper.ModeTest;
    }
}
