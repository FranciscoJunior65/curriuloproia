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
    private static readonly string[] ValidProviders = ["stripe", "mercadopago", "cakto", "kiwify"];
    private static readonly string[] ValidMercadoPagoModes =
        [MercadoPagoConfigHelper.ModeTest, MercadoPagoConfigHelper.ModeProduction];

    private readonly IAppSettingsRepository _settingsRepo;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SettingsService> _logger;

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

    public void ClearPaymentProviderCache()
    {
    }

    public async Task<string> GetPaymentProviderAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var value = await _settingsRepo.GetAppConfigValueAsync(PaymentProviderKey, cancellationToken);
            if (!string.IsNullOrEmpty(value))
            {
                return NormalizeProvider(value);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tabela app_configuracoes indisponível, usando PAYMENT_PROVIDER do .env");
        }

        return GetEnvDefault();
    }

    public async Task<string> SetPaymentProviderAsync(string provider, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeProvider(provider);

        try
        {
            await _settingsRepo.SetAppConfigValueAsync(PaymentProviderKey, normalized, cancellationToken);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Erro ao salvar provedor de pagamento: {ex.Message}", ex);
        }

        return normalized;
    }

    public IReadOnlyList<string> GetValidMercadoPagoModes() => ValidMercadoPagoModes;

    public void ClearMercadoPagoModeCache()
    {
    }

    public async Task<string> GetMercadoPagoModeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var value = await _settingsRepo.GetAppConfigValueAsync(MercadoPagoModeKey, cancellationToken);
            if (!string.IsNullOrEmpty(value))
            {
                return NormalizeMercadoPagoMode(value);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tabela app_configuracoes indisponível, usando MERCADOPAGO_MODE do .env");
        }

        return MercadoPagoConfigHelper.GetMode(_configuration);
    }

    public async Task<string> SetMercadoPagoModeAsync(string mode, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeMercadoPagoMode(mode);

        try
        {
            await _settingsRepo.SetAppConfigValueAsync(MercadoPagoModeKey, normalized, cancellationToken);
        }
        catch (Exception ex)
        {
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
