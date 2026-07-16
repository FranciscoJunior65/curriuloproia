using CurriculosProIA.Api.Infrastructure;
using CurriculosProIA.Repository.Interfaces;
using CurriculosProIA.Service.Helpers;
using CurriculosProIA.Service.Interfaces;
using CurriculosProIA.Domain.Dtos;
using CurriculosProIA.Domain.Signatures.Admin;
using CurriculosProIA.Api.Helpers;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mail;

namespace CurriculosProIA.Api.Controllers;

[ApiController]
[Route("api/test")]
public class TestController : ControllerBase
{
    private readonly ISupabaseConnectionTester _supabase;
    private readonly IAiService _ai;
    private readonly ISimliService _simli;
    private readonly IMercadoPagoService _mercadoPago;
    private readonly ICaktoService _cakto;
    private readonly IKiwifyService _kiwify;
    private readonly IEmailService _email;
    private readonly ISettingsService _settings;
    private readonly IPaymentRealtimeNotifier _paymentRealtime;
    private readonly IAppDataStore _data;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _hostEnvironment;

    public TestController(
        ISupabaseConnectionTester supabase,
        IAiService ai,
        ISimliService simli,
        IMercadoPagoService mercadoPago,
        ICaktoService cakto,
        IKiwifyService kiwify,
        IEmailService email,
        ISettingsService settings,
        IPaymentRealtimeNotifier paymentRealtime,
        IAppDataStore data,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment)
    {
        _supabase = supabase;
        _ai = ai;
        _simli = simli;
        _mercadoPago = mercadoPago;
        _cakto = cakto;
        _kiwify = kiwify;
        _email = email;
        _settings = settings;
        _paymentRealtime = paymentRealtime;
        _data = data;
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
                "5. GET /api/test/simli — valida avatar Simli (token na api.simli.ai)",
                "6. GET /api/test/email — envia e-mail de teste SMTP"
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

    /// <summary>Testa integração com Cakto (OAuth, oferta e webhook).</summary>
    [HttpGet("cakto")]
    public async Task<IActionResult> TestCakto(CancellationToken cancellationToken)
    {
        var missingMessage = CaktoConfigHelper.BuildMissingConfigMessage(_configuration);
        var debug = new
        {
            paymentProvider = await _settings.GetPaymentProviderAsync(cancellationToken),
            publicApiUrl = _configuration["PUBLIC_API_URL"]?.Trim() ?? "(localhost)",
            frontendUrl = _configuration["FRONTEND_URL"]?.Trim() ?? "http://localhost:4200",
            cakto = new
            {
                hasClientId = !string.IsNullOrWhiteSpace(CaktoConfigHelper.GetClientId(_configuration)),
                hasClientSecret = !string.IsNullOrWhiteSpace(CaktoConfigHelper.GetClientSecret(_configuration)),
                hasProductId = !string.IsNullOrWhiteSpace(CaktoConfigHelper.GetProductId(_configuration)),
                hasOfferId = !string.IsNullOrWhiteSpace(CaktoConfigHelper.GetOfferId(_configuration)),
                sdkClientIdPreview = CaktoConfigHelper.MaskSecret(CaktoConfigHelper.GetSdkClientId(_configuration)),
                clientIdPreview = CaktoConfigHelper.MaskSecret(CaktoConfigHelper.GetClientId(_configuration)),
                webhookSecretConfigured = !string.IsNullOrWhiteSpace(CaktoConfigHelper.GetWebhookSecret(_configuration)),
                missingMessage = string.IsNullOrWhiteSpace(missingMessage) ? null : missingMessage
            },
            envFile = EnvFileLoader.LoadedPath,
            envDiagnostics = EnvFileLoader.GetDiagnostics(_hostEnvironment.ContentRootPath)
        };

        if (!CaktoConfigHelper.HasApiCredentials(_configuration))
        {
            return StatusCode(500, new
            {
                success = false,
                connected = false,
                error = "Cakto não configurado",
                message = missingMessage,
                debug,
                help = new[]
                {
                    "1. Defina CAKTO_CLIENT_ID e CAKTO_CLIENT_SECRET no backend/.env",
                    "2. Crie produto + oferta no painel Cakto e preencha CAKTO_PRODUCT_ID e CAKTO_OFFER_ID",
                    "3. Rode GET /api/test/env para ver se o arquivo foi encontrado",
                    "4. Reinicie o site / app pool após alterar"
                }
            });
        }

        var result = await _cakto.TestConnectionAsync(cancellationToken);
        if (!result.Connected)
        {
            return StatusCode(500, new
            {
                success = false,
                connected = false,
                provider = result.Provider,
                error = "Falha na integração Cakto",
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

    /// <summary>Testa integração com Kiwify (OAuth, links de checkout e webhook).</summary>
    [HttpGet("kiwify")]
    public async Task<IActionResult> TestKiwify(CancellationToken cancellationToken)
    {
        var missingMessage = KiwifyConfigHelper.BuildMissingConfigMessage(_configuration);
        var debug = new
        {
            paymentProvider = await _settings.GetPaymentProviderAsync(cancellationToken),
            publicApiUrl = _configuration["PUBLIC_API_URL"]?.Trim() ?? "(localhost)",
            frontendUrl = _configuration["FRONTEND_URL"]?.Trim() ?? "http://localhost:4200",
            kiwify = new
            {
                hasApiKey = !string.IsNullOrWhiteSpace(KiwifyConfigHelper.GetApiKey(_configuration)),
                hasClientSecret = !string.IsNullOrWhiteSpace(KiwifyConfigHelper.GetClientSecret(_configuration)),
                hasAccountId = !string.IsNullOrWhiteSpace(KiwifyConfigHelper.GetAccountId(_configuration)),
                hasCheckoutSingle = !string.IsNullOrWhiteSpace(KiwifyConfigHelper.GetCheckoutCode(_configuration, "single", false)),
                hasCheckoutSingleEnglish = !string.IsNullOrWhiteSpace(KiwifyConfigHelper.GetCheckoutCode(_configuration, "single", true)),
                hasCheckoutPack3 = !string.IsNullOrWhiteSpace(KiwifyConfigHelper.GetCheckoutCode(_configuration, "pack3", false)),
                hasCheckoutPack3English = !string.IsNullOrWhiteSpace(KiwifyConfigHelper.GetCheckoutCode(_configuration, "pack3", true)),
                hasCheckoutPack5 = !string.IsNullOrWhiteSpace(KiwifyConfigHelper.GetCheckoutCode(_configuration, "pack5", false)),
                hasCheckoutPack5English = !string.IsNullOrWhiteSpace(KiwifyConfigHelper.GetCheckoutCode(_configuration, "pack5", true)),
                hasCheckoutEnglish = !string.IsNullOrWhiteSpace(KiwifyConfigHelper.GetCheckoutCode(_configuration, "english", false)),
                webhookTokenConfigured = !string.IsNullOrWhiteSpace(KiwifyConfigHelper.GetWebhookToken(_configuration)),
                apiKeyPreview = KiwifyConfigHelper.MaskSecret(KiwifyConfigHelper.GetApiKey(_configuration)),
                accountIdPreview = KiwifyConfigHelper.MaskSecret(KiwifyConfigHelper.GetAccountId(_configuration)),
                missingMessage = string.IsNullOrWhiteSpace(missingMessage) ? null : missingMessage
            },
            envFile = EnvFileLoader.LoadedPath,
            envDiagnostics = EnvFileLoader.GetDiagnostics(_hostEnvironment.ContentRootPath)
        };

        if (!KiwifyConfigHelper.HasApiCredentials(_configuration))
        {
            return StatusCode(500, new
            {
                success = false,
                connected = false,
                error = "Kiwify não configurado",
                message = missingMessage,
                debug,
                help = new[]
                {
                    "1. Defina KIWIFY_API_KEY (ou KIWIFY_CLIENT_ID), KIWIFY_CLIENT_SECRET e KIWIFY_ACCOUNT_ID no backend/.env",
                    "2. Reinicie a API após alterar o .env",
                    "3. Rode GET /api/test/env para ver se o arquivo foi encontrado",
                    "4. Se validar no admin de produção, copie as chaves Kiwify para o .env do servidor"
                }
            });
        }

        var result = await _kiwify.TestConnectionAsync(cancellationToken);
        if (!result.Connected)
        {
            return StatusCode(500, new
            {
                success = false,
                connected = false,
                provider = result.Provider,
                error = "Falha na integração Kiwify",
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

    /// <summary>Consulta venda na Kiwify (order_ref ou order_id). Requer admin.</summary>
    [HttpGet("kiwify/sale/{orderId}")]
    public async Task<IActionResult> GetKiwifySale(string orderId, CancellationToken cancellationToken)
    {
        if (!await EnsureAdminAsync(cancellationToken))
        {
            return Unauthorized(new { success = false, error = "Acesso negado — requer token de administrador" });
        }

        if (string.IsNullOrWhiteSpace(orderId))
        {
            return BadRequest(new { success = false, error = "orderId é obrigatório" });
        }

        try
        {
            var sale = await _kiwify.GetSaleDetailsAsync(orderId.Trim(), cancellationToken);
            return Ok(new { success = true, sale });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>Dispara evento paymentConfirmed no hub SignalR (teste). Requer admin.</summary>
    [HttpPost("payment-hub")]
    public async Task<IActionResult> TestPaymentHub(
        [FromBody] TestPaymentHubSignature? body,
        CancellationToken cancellationToken)
    {
        if (!await EnsureAdminAsync(cancellationToken))
        {
            return Unauthorized(new { success = false, error = "Acesso negado — requer token de administrador" });
        }

        var callerUserId = JwtAuthHelper.TryGetUserId(Request.Headers, _configuration);
        var targetUserId = string.IsNullOrWhiteSpace(body?.UserId) ? callerUserId : body!.UserId!.Trim();
        if (string.IsNullOrWhiteSpace(targetUserId))
        {
            return BadRequest(new { success = false, error = "Informe userId no body ou use token de usuário logado" });
        }

        var credits = body?.Credits > 0 ? body.Credits : 1;
        var profile = await _data.GetUserProfileAsync(targetUserId, cancellationToken);
        var currentCredits = profile != null
            ? await _data.GetUserCreditsAsync(targetUserId, cancellationToken)
            : credits;

        await _paymentRealtime.NotifyPaymentConfirmedAsync(
            new PaymentConfirmedNotification
            {
                UserId = targetUserId,
                Credits = currentCredits,
                OrderId = $"test_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}",
                PlanId = "test",
                Provider = "test",
                AlreadyFulfilled = false
            },
            cancellationToken);

        return Ok(new
        {
            success = true,
            message = body?.Message ?? "Evento paymentConfirmed enviado ao hub.",
            userId = targetUserId,
            credits = currentCredits,
            hubPath = "/hubs/payment",
            eventName = "paymentConfirmed"
        });
    }

    private async Task<bool> EnsureAdminAsync(CancellationToken cancellationToken)
    {
        var userId = JwtAuthHelper.TryGetUserId(Request.Headers, _configuration);
        if (string.IsNullOrEmpty(userId))
        {
            return false;
        }

        var profile = await _data.GetUserProfileAsync(userId, cancellationToken);
        return profile?.UserType == "admin";
    }

    /// <summary>Envia e-mail de teste SMTP (diagnóstico). Query opcional: ?to=destino@email.com</summary>
    [HttpGet("email")]
    public async Task<IActionResult> TestEmail([FromQuery] string? to, CancellationToken cancellationToken)
    {
        var recipient = string.IsNullOrWhiteSpace(to)
            ? _configuration["EMAIL_BCC_TO"]?.Trim() ?? "juniorbx@gmail.com"
            : to.Trim();

        try
        {
            _ = new MailAddress(recipient);
        }
        catch (FormatException)
        {
            return BadRequest(new
            {
                success = false,
                error = "E-mail de destino inválido",
                message = "Use ?to=seu@email.com ou omita para enviar ao EMAIL_BCC_TO do .env"
            });
        }

        var sender = _configuration["EMAIL_SENDER"]?.Trim() ?? _configuration["EMAIL_USER"]?.Trim();
        var smtpHost = _configuration["SMTP_HOST"]?.Trim() ?? _configuration["EMAIL_HOST"]?.Trim();
        var smtpAlt = _configuration["SMTP_HOST_ALTERNATIVE"]?.Trim()
            ?? _configuration["SMTP_HOST_ALTERNATIVO"]?.Trim();
        var hasPassword = !string.IsNullOrWhiteSpace(
            _configuration["EMAIL_SENDER_PASSWORD"]?.Trim() ?? _configuration["EMAIL_PASSWORD"]?.Trim());

        var config = new
        {
            envFile = EnvFileLoader.LoadedPath,
            smtpHost = string.IsNullOrEmpty(smtpHost) ? "(ausente)" : smtpHost,
            smtpHostAlternative = string.IsNullOrEmpty(smtpAlt) ? "(ausente)" : smtpAlt,
            smtpPort = _configuration["SMTP_PORT"]?.Trim() ?? _configuration["EMAIL_PORT"]?.Trim() ?? "587",
            sender = string.IsNullOrEmpty(sender) ? "(ausente)" : sender,
            senderName = _configuration["EMAIL_SENDER_NAME"]?.Trim() ?? "CurriculosPro IA",
            hasPassword,
            bccDefault = _configuration["EMAIL_BCC_TO"]?.Trim() ?? "juniorbx@gmail.com",
            ccCopy = _configuration["EMAIL_COPY_TO"]?.Trim() ?? _configuration["EMAIL_COPY"]?.Trim()
        };

        if (string.IsNullOrEmpty(sender) || !hasPassword || string.IsNullOrEmpty(smtpHost))
        {
            return StatusCode(500, new
            {
                success = false,
                error = "SMTP não configurado",
                message = "Defina SMTP_HOST, EMAIL_SENDER e EMAIL_SENDER_PASSWORD no backend/.env",
                config,
                help = new[]
                {
                    "1. Edite backend/.env com SMTP_HOST, EMAIL_SENDER, EMAIL_SENDER_PASSWORD",
                    "2. Senhas com # devem ficar entre aspas: EMAIL_SENDER_PASSWORD=\"senha#123\"",
                    "3. Reinicie a API / app pool",
                    "4. GET /api/test/email?to=seu@email.com"
                }
            });
        }

        try
        {
            await _email.SendTestEmailAsync(recipient, cancellationToken);
            return Ok(new
            {
                success = true,
                message = $"E-mail de teste enviado para {recipient} (BCC automático em {config.bccDefault})",
                sentTo = recipient,
                config,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                error = "Falha ao enviar e-mail de teste",
                message = ex.Message,
                sentTo = recipient,
                config,
                help = new[]
                {
                    "1. Confirme SMTP_HOST (ex.: mail.getpushtecnologia.com.br) e porta 587",
                    "2. Teste login SMTP no webmail / painel de hospedagem",
                    "3. Verifique se a senha no .env está entre aspas se contiver #",
                    "4. Reinicie a API após alterar o .env"
                }
            });
        }
    }
}
