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
    private readonly ISimliService _simli;
    private readonly IMercadoPagoService _mercadoPago;
    private readonly ISettingsService _settings;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _hostEnvironment;

    public TestController(
        ISupabaseConnectionTester supabase,
        IAiService ai,
        ISimliService simli,
        IMercadoPagoService mercadoPago,
        ISettingsService settings,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment)
    {
        _supabase = supabase;
        _ai = ai;
        _simli = simli;
        _mercadoPago = mercadoPago;
        _settings = settings;
        _configuration = configuration;
        _hostEnvironment = hostEnvironment;
    }

    /// <summary>Diagnóstico do carregamento do backend/.env (mesmo arquivo no localhost e no servidor).</summary>
    [HttpGet("env")]
    public IActionResult TestEnv()
    {
        var diagnostics = EnvFileLoader.GetDiagnostics(_hostEnvironment.ContentRootPath);
        var loaded = EnvFileLoader.LoadedPath != null && System.IO.File.Exists(EnvFileLoader.LoadedPath);

        return Ok(new
        {
            success = loaded || EnvFileLoader.HasSupabaseEnvironmentVariables(),
            message = loaded
                ? $"Arquivo carregado: {EnvFileLoader.LoadedPath}"
                : "backend/.env não encontrado. Crie em backend/.env (copie ENV_EXAMPLE.env) e republique.",
            diagnostics,
            help = new[]
            {
                "1. Um único arquivo: backend/.env (localhost e servidor)",
                "2. Publish inclui automaticamente como .env na pasta do site",
                "3. Reinicie o app pool após publicar",
                "4. GET /api/test/mercadopago — valida Mercado Pago",
                "5. GET /api/test/simli — valida avatar Simli (token na api.simli.ai)"
            }
        });
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
                    "SERVIDOR: backend/.env vai no publish como .env (mesma pasta do .dll)",
                    "  Ex.: F:\\Inetpub\\vhosts\\...\\api.curriculoproia.com.br\\.env",
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

    /// <summary>Testa token real na api.simli.ai (servidor → Simli). Não depende do navegador/WebRTC.</summary>
    [HttpGet("simli")]
    public async Task<IActionResult> TestSimli(CancellationToken cancellationToken)
    {
        var apiKey = _configuration["SIMLI_API_KEY"]?.Trim() ?? "";
        var config = _simli.GetConfig();
        var debug = new
        {
            hasApiKey = !string.IsNullOrWhiteSpace(apiKey),
            apiKeyPreview = string.IsNullOrWhiteSpace(apiKey) ? "não definido" : $"{apiKey[..Math.Min(8, apiKey.Length)]}...",
            apiKeyLength = apiKey.Length,
            enabled = config.Enabled,
            transportMode = config.TransportMode,
            defaultFaceId = config.DefaultFaceId,
            envFile = EnvFileLoader.LoadedPath
        };

        if (!config.Enabled)
        {
            return StatusCode(500, new
            {
                success = false,
                error = "Simli não configurado",
                message = "SIMLI_API_KEY ausente ou vazia no .env carregado pelo servidor.",
                debug,
                help = new[]
                {
                    "1. Edite backend/.env — SIMLI_API_KEY=sua-chave (https://simli.com)",
                    "2. Republique a API (o .env vai na pasta do site)",
                    "3. Reinicie o app pool no Plesk",
                    "4. Se SIMLI_API_KEY existir no painel do Plesk, remova ou atualize — o .env da pasta do site prevalece"
                }
            });
        }

        try
        {
            var start = DateTime.UtcNow;
            var session = await _simli.CreateSessionAsync(null, null, cancellationToken);
            var elapsedMs = (int)(DateTime.UtcNow - start).TotalMilliseconds;

            return Ok(new
            {
                success = true,
                message = "Conexão servidor → Simli OK (session_token gerado).",
                faceId = session.FaceId,
                sessionTokenPreview = $"{session.SessionToken[..Math.Min(16, session.SessionToken.Length)]}...",
                responseTime = $"{elapsedMs}ms",
                debug,
                connection = "OK",
                note = "Se este teste passar mas o vídeo falhar no navegador, o problema é WebRTC/LiveKit (rede do cliente), não a chave.",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                error = "Erro ao conectar com Simli",
                message = ex.Message,
                debug,
                connection = "ERRO",
                help = new[]
                {
                    "1. Confirme SIMLI_API_KEY em backend/.env e republique",
                    "2. Verifique se o servidor consegue acessar https://api.simli.ai (firewall/saída HTTPS)",
                    "3. Reinicie o app pool após alterar o .env"
                }
            });
        }
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
                    "3. Edite backend/.env — GEMINI_API_KEY=chave real (https://aistudio.google.com/apikey)",
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
        var resolvedMode = await _settings.GetMercadoPagoModeAsync(cancellationToken);
        var token = MercadoPagoConfigHelper.GetAccessToken(_configuration, resolvedMode)?.Trim() ?? string.Empty;
        var tokenKey = MercadoPagoConfigHelper.GetAccessTokenEnvKey(resolvedMode);
        var debug = new
        {
            hasAccessToken = !string.IsNullOrWhiteSpace(token),
            tokenPreview = string.IsNullOrWhiteSpace(token) ? "não definido" : MercadoPagoConfigHelper.MaskToken(token),
            resolvedMode,
            requiredEnvKey = tokenKey,
            paymentProvider = await _settings.GetPaymentProviderAsync(cancellationToken),
            publicApiUrl = _configuration["PUBLIC_API_URL"]?.Trim() ?? "(localhost)",
            frontendUrl = _configuration["FRONTEND_URL"]?.Trim() ?? "http://localhost:4200",
            mercadoPago = MercadoPagoConfigHelper.GetDebugInfo(_configuration, resolvedMode),
            envFile = EnvFileLoader.LoadedPath,
            envDiagnostics = EnvFileLoader.GetDiagnostics(_hostEnvironment.ContentRootPath)
        };

        if (string.IsNullOrWhiteSpace(token))
        {
            return StatusCode(500, new
            {
                success = false,
                connected = false,
                error = "Mercado Pago não configurado",
                message = MercadoPagoConfigHelper.BuildMissingTokenMessage(resolvedMode),
                debug,
                help = new[]
                {
                    $"1. Modo ativo: {resolvedMode} (MERCADOPAGO_MODE no backend/.env)",
                    $"2. Defina {tokenKey} no backend/.env",
                    "3. Rode GET /api/test/env para ver se o arquivo foi encontrado",
                    "4. Reinicie o site / app pool após alterar"
                }
            });
        }

        var result = await _mercadoPago.TestConnectionAsync(cancellationToken);
        if (!result.Connected)
        {
            var misconfigured = result.Message.Contains("CONFIGURAÇÃO INVÁLIDA", StringComparison.Ordinal);
            return StatusCode(misconfigured ? 503 : 500, new
            {
                success = false,
                connected = false,
                provider = result.Provider,
                error = misconfigured ? "Mercado Pago mal configurado" : "Falha na integração Mercado Pago",
                message = result.Message,
                details = result.Details,
                debug,
                connection = "ERRO",
                fix = misconfigured
                    ? new[]
                    {
                        "1. Painel admin → Produção (ou MERCADOPAGO_MODE=production no .env)",
                        "2. backend/.env → MERCADOPAGO_ACCESS_TOKEN_PRODUCTION com token REAL de produção",
                        "3. Republique a API e reinicie o app pool"
                    }
                    : null
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
