using CurriculosProIA.Api.Infrastructure;
using CurriculosProIA.Repository.Interfaces;
using CurriculosProIA.Service.Helpers;
using CurriculosProIA.Service.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CurriculosProIA.Api.Controllers;

[ApiController]
[Route("api/test")]
public class TestController : ControllerBase
{
    private readonly ISupabaseConnectionTester _supabase;
    private readonly IAiService _ai;
    private readonly IMercadoPagoService _mercadoPago;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _hostEnvironment;

    public TestController(
        ISupabaseConnectionTester supabase,
        IAiService ai,
        IMercadoPagoService mercadoPago,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment)
    {
        _supabase = supabase;
        _ai = ai;
        _mercadoPago = mercadoPago;
        _configuration = configuration;
        _hostEnvironment = hostEnvironment;
    }

    /// <summary>Testa conexão com Supabase (equivalente ao GET /api/test/supabase da API Node).</summary>
    [HttpGet("supabase")]
    public async Task<IActionResult> TestSupabase(CancellationToken cancellationToken)
    {
        var config = _supabase.GetConfigurationStatus();
        if (!config.Configured)
        {
            var url = _configuration["SUPABASE_URL"]?.Trim() ?? "";
            var key = _configuration["SUPABASE_SERVICE_ROLE_KEY"]?.Trim() ?? "";
            var isPlaceholderUrl = !string.IsNullOrEmpty(url) &&
                url.Contains("seu-projeto.supabase.co", StringComparison.OrdinalIgnoreCase);
            var isPlaceholderKey = !string.IsNullOrEmpty(key) &&
                (key == "sua_service_role_key_aqui" || key.StartsWith("sua_", StringComparison.Ordinal) || key.Length < 40);

            return StatusCode(500, new
            {
                success = false,
                error = "Supabase não configurado",
                message = config.Message,
                envFile = EnvFileLoader.LoadedPath,
                envFileExists = EnvFileLoader.LoadedPath != null && System.IO.File.Exists(EnvFileLoader.LoadedPath),
                searchedPaths = EnvFileLoader.LastSearchedPaths.Take(8),
                hasSupabaseUrl = !string.IsNullOrEmpty(url),
                hasSupabaseServiceKey = !string.IsNullOrEmpty(key),
                usingPlaceholderValues = isPlaceholderUrl || isPlaceholderKey,
                contentRoot = Directory.GetCurrentDirectory(),
                appBaseDirectory = AppContext.BaseDirectory,
                help = new[]
                {
                    "SERVIDOR (IIS/Plesk): copie backend/.env para a pasta do site como .env ou app.env",
                    "  Ex.: F:\\Inetpub\\vhosts\\...\\api.curriculoproia.com.br\\app.env (mesma pasta do .dll)",
                    "  Ou defina SUPABASE_URL e SUPABASE_SERVICE_ROLE_KEY nas variáveis de ambiente do Plesk",
                    "DEV: edite backend/.env com os valores do Supabase → Settings → API",
                    "Reinicie o site / app pool após alterar"
                }
            });
        }

        var result = await _supabase.TestConnectionAsync(cancellationToken);
        if (!result.Success)
        {
            return StatusCode(500, new
            {
                success = false,
                error = result.Message,
                details = result.Error,
                envFile = EnvFileLoader.LoadedPath
            });
        }

        return Ok(new
        {
            success = true,
            message = result.Message,
            warning = result.Warning,
            profileSampleCount = result.ProfileCount,
            connection = "OK",
            envFile = EnvFileLoader.LoadedPath
        });
    }

    /// <summary>Testa conexão real com Gemini (equivalente ao GET /api/test/gemini da API Node).</summary>
    [HttpGet("gemini")]
    public async Task<IActionResult> TestGemini(CancellationToken cancellationToken)
    {
        var apiKey = _configuration["GEMINI_API_KEY"]?.Trim() ?? "";
        var useMock = AiRuntimeOptions.UseMockAi(_configuration, _hostEnvironment);
        var model = _configuration["GEMINI_MODEL"] ?? "gemini-3-flash-preview";
        if (model == "gemini-pro")
        {
            model = "gemini-3-flash-preview";
        }

        var debug = new
        {
            hasApiKey = !string.IsNullOrWhiteSpace(apiKey),
            apiKeyPreview = string.IsNullOrWhiteSpace(apiKey) ? "não definido" : $"{apiKey[..Math.Min(20, apiKey.Length)]}...",
            apiKeyLength = apiKey.Length,
            provider = _configuration["AI_PROVIDER"] ?? "gemini",
            model,
            useMockAi = useMock,
            envFile = EnvFileLoader.LoadedPath
        };

        if (useMock)
        {
            return StatusCode(500, new
            {
                success = false,
                error = "Modo mock ativo",
                message = "USE_MOCK_AI está true (apenas em Development). Em Production o mock é ignorado; ajuste o .env ou ASPNETCORE_ENVIRONMENT.",
                debug
            });
        }

        var invalidKeyReason = GeminiConfigHelper.GetInvalidKeyReason(apiKey);
        if (invalidKeyReason != null)
        {
            return StatusCode(500, new
            {
                success = false,
                error = "Gemini não configurado",
                message = invalidKeyReason,
                debug,
                help = new[]
                {
                    "1. Acesse https://aistudio.google.com/apikey",
                    "2. Crie uma API key (grátis)",
                    "3. No servidor: edite app.env — GEMINI_API_KEY=chave real (https://aistudio.google.com/apikey)",
                    "4. USE_MOCK_AI=false (opcional em Production; mock já é desligado automaticamente)",
                    "5. Reinicie o site / app pool no Plesk"
                }
            });
        }

        try
        {
            var start = DateTime.UtcNow;
            var response = await _ai.GenerateTextAsync(
                "Responda apenas com a palavra OK, sem mais nada.",
                temperature: 0.1,
                maxOutputTokens: 256,
                cancellationToken);
            var elapsedMs = (int)(DateTime.UtcNow - start).TotalMilliseconds;

            return Ok(new
            {
                success = true,
                message = "Conexão com Gemini OK!",
                response = response.Trim(),
                responseTime = $"{elapsedMs}ms",
                debug,
                connection = "OK",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                error = "Erro ao conectar com Gemini",
                message = ex.Message,
                debug,
                connection = "ERRO"
            });
        }
    }

    /// <summary>Testa integração com Mercado Pago (credenciais, conta e webhook).</summary>
    [HttpGet("mercadopago")]
    public async Task<IActionResult> TestMercadoPago(CancellationToken cancellationToken)
    {
        var token = MercadoPagoConfigHelper.GetAccessToken(_configuration)?.Trim() ?? string.Empty;
        var debug = new
        {
            hasAccessToken = !string.IsNullOrWhiteSpace(token),
            tokenPreview = string.IsNullOrWhiteSpace(token) ? "não definido" : MercadoPagoConfigHelper.MaskToken(token),
            paymentProvider = _configuration["PAYMENT_PROVIDER"] ?? "stripe",
            publicApiUrl = _configuration["PUBLIC_API_URL"]?.Trim() ?? "(localhost)",
            frontendUrl = _configuration["FRONTEND_URL"]?.Trim() ?? "http://localhost:4200",
            mercadoPago = MercadoPagoConfigHelper.GetDebugInfo(_configuration),
            envFile = EnvFileLoader.LoadedPath
        };

        if (string.IsNullOrWhiteSpace(token))
        {
            return StatusCode(500, new
            {
                success = false,
                connected = false,
                error = "Mercado Pago não configurado",
                message = "Configure MERCADOPAGO_MODE=test|production e os tokens _TEST/_PRODUCTION no .env",
                debug,
                help = new[]
                {
                    "1. MERCADOPAGO_MODE=test (desenvolvimento) ou production (pagamentos reais)",
                    "2. Preencha MERCADOPAGO_ACCESS_TOKEN_TEST e MERCADOPAGO_ACCESS_TOKEN_PRODUCTION",
                    "3. Reinicie o backend"
                }
            });
        }

        var result = await _mercadoPago.TestConnectionAsync(cancellationToken);
        if (!result.Connected)
        {
            return StatusCode(500, new
            {
                success = false,
                connected = false,
                provider = result.Provider,
                error = "Falha na integração Mercado Pago",
                message = result.Message,
                details = result.Details,
                debug,
                connection = "ERRO"
            });
        }

        return Ok(new
        {
            success = true,
            connected = true,
            provider = result.Provider,
            message = result.Message,
            details = result.Details,
            debug,
            connection = "OK",
            timestamp = DateTime.UtcNow
        });
    }
}
