using CurriculosProIA.Api.Infrastructure;
using CurriculosProIA.Repository.Interfaces;
using CurriculosProIA.Service.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CurriculosProIA.Api.Controllers;

[ApiController]
[Route("api/test")]
public class TestController : ControllerBase
{
    private readonly ISupabaseConnectionTester _supabase;
    private readonly IAiService _ai;
    private readonly IConfiguration _configuration;

    public TestController(
        ISupabaseConnectionTester supabase,
        IAiService ai,
        IConfiguration configuration)
    {
        _supabase = supabase;
        _ai = ai;
        _configuration = configuration;
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
            var isPlaceholderUrl = url.Contains("seu-projeto.supabase.co", StringComparison.OrdinalIgnoreCase);
            var isPlaceholderKey = key is "sua_service_role_key_aqui" or { Length: < 40 };

            return StatusCode(500, new
            {
                success = false,
                error = "Supabase não configurado",
                message = config.Message,
                envFile = EnvFileLoader.LoadedPath,
                envFileExists = EnvFileLoader.LoadedPath != null && System.IO.File.Exists(EnvFileLoader.LoadedPath),
                hasSupabaseUrl = !string.IsNullOrEmpty(url),
                hasSupabaseServiceKey = !string.IsNullOrEmpty(key),
                usingPlaceholderValues = isPlaceholderUrl || isPlaceholderKey,
                currentDirectory = Directory.GetCurrentDirectory(),
                help = new[]
                {
                    "1. Edite o arquivo backend/.env (já criado se você rodou scripts/setup-env.ps1)",
                    "2. Cole SUPABASE_URL e SUPABASE_SERVICE_ROLE_KEY do painel Supabase → Settings → API",
                    "3. Use os MESMOS valores que funcionam na API Node (backend-node/.env)",
                    "4. Reinicie a API .NET (pare o Visual Studio e suba de novo)"
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
        var useMock = _configuration["USE_MOCK_AI"] is "true" or "1";
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
                message = "USE_MOCK_AI está true. Defina USE_MOCK_AI=false no .env para testar a IA real.",
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
                    "3. Edite backend/.env e substitua GEMINI_API_KEY=sua-chave-gemini-aqui pela chave real",
                    "4. Reinicie a API após salvar"
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
}
