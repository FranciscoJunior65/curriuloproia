using System.Globalization;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

using CurriculosProIA.Service.Interfaces;
using CurriculosProIA.Repository.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CurriculosProIA.Service.Implementations;

public class EmailService : IEmailService
{
    private const string AppNameVerification = "CurriculoPro IA";
    private const string AppNameBranded = "CurriculosPro IA";

    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public string GenerateVerificationCode() =>
        Random.Shared.Next(100000, 1000000).ToString(CultureInfo.InvariantCulture);

    public Task SendVerificationEmailAsync(string email, string code, string name = "", CancellationToken cancellationToken = default)
    {
        var greeting = FormatGreeting(name);
        var year = DateTime.UtcNow.Year;

        var html = $"""
            <!DOCTYPE html>
            <html>
            <head><meta charset="utf-8"></head>
            <body style="font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;">
              <div class="container" style="background: #f9f9f9; border-radius: 8px; padding: 30px;">
                <h1 style="color: #4CAF50; text-align: center;">{AppNameVerification}</h1>
                <p>{greeting}</p>
                <p>Obrigado por se cadastrar no {AppNameVerification}. Para completar seu cadastro, use o código de verificação abaixo:</p>
                <div style="background: #fff; border: 2px dashed #4CAF50; border-radius: 8px; padding: 20px; text-align: center; margin: 30px 0;">
                  <div style="font-size: 32px; font-weight: bold; color: #4CAF50; letter-spacing: 8px;">{code}</div>
                </div>
                <p><strong>Este código expira em 15 minutos.</strong></p>
                <p>Se você não solicitou este código, ignore este email.</p>
                <p style="font-size: 12px; color: #666; text-align: center; margin-top: 30px;">&copy; {year} GetPush Tecnologia. Todos os direitos reservados.</p>
              </div>
            </body>
            </html>
            """;

        var text = $"""
            {greeting}

            Obrigado por se cadastrar no {AppNameVerification}.

            Seu código de verificação é: {code}

            Este código expira em 15 minutos.

            Se você não solicitou este código, ignore este email.
            """;

        return SendAsync(
            email,
            $"🔐 Código de Verificação - {AppNameVerification}",
            html,
            text,
            useCc: true,
            cancellationToken: cancellationToken);
    }

    public Task SendWelcomeEmailAsync(string email, string name = "", CancellationToken cancellationToken = default)
    {
        var frontendUrl = GetFrontendUrl();
        var greeting = FormatGreeting(name);
        var year = DateTime.UtcNow.Year;

        var html = $"""
            <!DOCTYPE html>
            <html><head><meta charset="utf-8"></head>
            <body style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;">
              <div style="background: #f9f9f9; border-radius: 8px; padding: 30px;">
                <h1 style="color: #4CAF50; text-align: center;">{AppNameVerification}</h1>
                <p>{greeting}</p>
                <p>Bem-vindo ao {AppNameVerification}! Sua conta foi criada com sucesso.</p>
                <p style="text-align: center;"><a href="{frontendUrl}" style="display: inline-block; padding: 12px 30px; background: #4CAF50; color: white; text-decoration: none; border-radius: 5px;">Acessar Plataforma</a></p>
                <p style="font-size: 12px; color: #666; text-align: center;">&copy; {year} GetPush Tecnologia.</p>
              </div>
            </body></html>
            """;

        var text = $"{greeting}\n\nBem-vindo ao {AppNameVerification}! Acesse: {frontendUrl}";

        return SendAsync(email, $"🎉 Bem-vindo ao {AppNameVerification}!", html, text, useCc: true, cancellationToken: cancellationToken);
    }

    public Task SendLoginNotificationEmailAsync(string email, string name = "", CancellationToken cancellationToken = default)
    {
        var greeting = FormatGreeting(name);
        var now = FormatBrazilDateTime();
        var year = DateTime.UtcNow.Year;

        var html = $"""
            <!DOCTYPE html>
            <html><head><meta charset="utf-8"></head>
            <body style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;">
              <div style="background: #f9f9f9; border-radius: 8px; padding: 30px;">
                <h2 style="color: #4CAF50;">Login Realizado</h2>
                <p>{greeting}</p>
                <p>Identificamos um novo login na sua conta do {AppNameVerification}.</p>
                <div style="background: #fff3cd; border-left: 4px solid #ffc107; padding: 15px; margin: 20px 0;">
                  <strong>Detalhes do acesso:</strong><br>Data e hora: {now}
                </div>
                <p style="font-size: 12px; color: #666; text-align: center;">&copy; {year} GetPush Tecnologia.</p>
              </div>
            </body></html>
            """;

        var text = $"{greeting}\n\nNovo login em {AppNameVerification}.\nData e hora: {now}";

        return SendAsync(email, $"🔐 Login Realizado - {AppNameVerification}", html, text, useCc: true, cancellationToken: cancellationToken);
    }

    public Task SendVerificationLinkEmailAsync(string email, string token, string name = "", CancellationToken cancellationToken = default)
    {
        var frontendUrl = GetFrontendUrl();
        var link = $"{frontendUrl}/verify-email?token={token}&email={Uri.EscapeDataString(email)}";
        var greeting = FormatGreeting(name);
        var year = DateTime.UtcNow.Year;

        var html = $"""
            <!DOCTYPE html>
            <html><head><meta charset="utf-8"></head>
            <body style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;">
              <div style="background: #f9f9f9; border-radius: 8px; padding: 30px;">
                <h2 style="color: #4CAF50;">Verifique seu email</h2>
                <p>{greeting}</p>
                <p>Você já possui uma conta no {AppNameVerification}, mas seu email ainda não foi verificado.</p>
                <p style="text-align: center;"><a href="{link}" style="display: inline-block; padding: 12px 30px; background: #4CAF50; color: white; text-decoration: none; border-radius: 5px;">Verificar Email</a></p>
                <p style="word-break: break-all; font-size: 12px; color: #666;">{link}</p>
                <p><strong>Este link expira em 24 horas.</strong></p>
                <p style="font-size: 12px; color: #666; text-align: center;">&copy; {year} GetPush Tecnologia.</p>
              </div>
            </body></html>
            """;

        var text = $"{greeting}\n\nVerifique seu email: {link}\n\nEste link expira em 24 horas.";

        return SendAsync(email, $"🔗 Verifique seu email - {AppNameVerification}", html, text, useCc: true, cancellationToken: cancellationToken);
    }

    public Task SendPasswordResetEmailAsync(string email, string token, string name = "", CancellationToken cancellationToken = default)
    {
        var resetLink = $"{GetFrontendUrl()}/login?token={token}";
        var greeting = FormatGreeting(name);
        var year = DateTime.UtcNow.Year;

        var html = $"""
            <!DOCTYPE html>
            <html><head><meta charset="utf-8"></head>
            <body style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;">
              <div style="background: #f9f9f9; border-radius: 8px; padding: 30px;">
                <h2 style="color: #6366f1;">Recuperação de Senha</h2>
                <p>{greeting}</p>
                <p>Recebemos uma solicitação para redefinir a senha da sua conta no <strong>{AppNameBranded}</strong>.</p>
                <p style="text-align: center;"><a href="{resetLink}" style="display: inline-block; padding: 12px 30px; background: linear-gradient(to right, #6366f1, #8b5cf6); color: white; text-decoration: none; border-radius: 6px;">Redefinir Senha</a></p>
                <p style="word-break: break-all; color: #6366f1;">{resetLink}</p>
                <p><strong>Este link expira em 1 hora.</strong></p>
                <p style="font-size: 12px; color: #666; text-align: center;">&copy; {year} {AppNameBranded}.</p>
              </div>
            </body></html>
            """;

        var text = $"{greeting}\n\nRedefina sua senha: {resetLink}\n\nEste link expira em 1 hora.";

        return SendAsync(email, $"🔐 Recuperação de Senha - {AppNameBranded}", html, text, useCc: true, cancellationToken: cancellationToken);
    }

    public Task SendPasswordChangeNotificationEmailAsync(string email, string name = "", CancellationToken cancellationToken = default)
    {
        var greeting = FormatGreeting(name);
        var now = FormatBrazilDateTimeDetailed();
        var year = DateTime.UtcNow.Year;

        var html = $"""
            <!DOCTYPE html>
            <html><head><meta charset="utf-8"></head>
            <body style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;">
              <div style="background: #f9f9f9; border-radius: 8px; padding: 30px;">
                <h2 style="color: #4CAF50;">✅ Senha Alterada com Sucesso</h2>
                <p>{greeting}</p>
                <p>Sua senha foi alterada com sucesso na sua conta do <strong>{AppNameBranded}</strong>.</p>
                <p>Data e hora: <strong>{now}</strong></p>
                <p style="font-size: 12px; color: #666; text-align: center;">&copy; {year} {AppNameBranded}.</p>
              </div>
            </body></html>
            """;

        var text = $"{greeting}\n\nSua senha foi alterada em {now}.";

        return SendAsync(email, $"🔐 Senha alterada - {AppNameBranded}", html, text, useCc: true, cancellationToken: cancellationToken);
    }

    public Task SendLoginCodeEmailAsync(string email, string code, string name = "", CancellationToken cancellationToken = default)
    {
        var greeting = FormatGreeting(name);
        var year = DateTime.UtcNow.Year;

        var html = $"""
            <!DOCTYPE html>
            <html><head><meta charset="utf-8"></head>
            <body style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;">
              <div style="background: #f9f9f9; border-radius: 8px; padding: 30px;">
                <h2 style="color: #6366f1;">Código de Login</h2>
                <p>{greeting}</p>
                <div style="background: linear-gradient(to right, #6366f1, #8b5cf6); color: white; font-size: 32px; font-weight: bold; text-align: center; padding: 20px; border-radius: 8px; letter-spacing: 8px;">{code}</div>
                <p><strong>Este código expira em 10 minutos.</strong></p>
                <p style="font-size: 12px; color: #666; text-align: center;">&copy; {year} {AppNameBranded}.</p>
              </div>
            </body></html>
            """;

        var text = $"{greeting}\n\nCódigo de login: {code}\n\nExpira em 10 minutos.";

        return SendAsync(email, $"🔐 Seu código de login - {AppNameBranded}", html, text, useCc: true, cancellationToken: cancellationToken);
    }

    public async Task SendPurchaseConfirmationEmailAsync(
        string clientEmail,
        PurchaseConfirmationDetails details,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var credits = details.CreditsAmount ?? details.Analyses;
            var priceStr = FormatPrice(details.Price);
            var usedCoupon = !string.IsNullOrEmpty(details.CouponName) &&
                (details.DiscountPercent != null || details.OriginalPrice != null);
            var originalPriceStr = details.OriginalPrice != null ? FormatPrice(details.OriginalPrice) : "";
            var discountPctStr = details.DiscountPercent?.ToString() ?? "";
            var now = FormatBrazilDateTimeDetailed();
            var greeting = string.IsNullOrWhiteSpace(details.CustomerName)
                ? "Olá!"
                : $"Olá, {details.CustomerName}!";
            var frontendUrl = GetFrontendUrl().TrimEnd('/');

            var couponHtml = usedCoupon
                ? $"""<div style="margin-top: 12px; padding: 10px; background: #fff8e1; border-radius: 6px;"><strong>🎟️ Cupom:</strong> {details.CouponName} — <strong>{discountPctStr}% de desconto</strong>{(string.IsNullOrEmpty(originalPriceStr) ? "" : $" (preço original: R$ {originalPriceStr})")}</div>"""
                : "";

            var creditsHtml = credits != null
                ? $"""<div><strong>Créditos de análise:</strong> {credits}</div>"""
                : "";

            var siteUrl = $"{frontendUrl}/";
            var html = $"""
                <!DOCTYPE html>
                <html><head><meta charset="utf-8"></head>
                <body style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;">
                  <div style="background: #f9f9f9; border-radius: 8px; padding: 30px;">
                    <h2 style="color: #4CAF50;">✅ Compra confirmada!</h2>
                    <p>{greeting}</p>
                    <div style="background: #e8f5e9; padding: 16px; border-radius: 8px;">
                      <div><strong>Plano:</strong> {details.PlanName}</div>
                      {creditsHtml}
                      {couponHtml}
                      <div><strong>Valor pago:</strong> R$ {priceStr}</div>
                      <div><strong>Data:</strong> {now}</div>
                      {(string.IsNullOrEmpty(details.ExtraInfo) ? "" : $"<p>{details.ExtraInfo}</p>")}
                    </div>
                    <table role="presentation" cellspacing="0" cellpadding="0" border="0" align="center" style="margin: 28px auto 0;">
                      <tr>
                        <td style="border-radius: 8px; background: #4f46e5;">
                          <a href="{siteUrl}" target="_blank" rel="noopener noreferrer"
                             style="display: inline-block; padding: 16px 36px; font-size: 16px; font-weight: 700; color: #ffffff; text-decoration: none; border-radius: 8px;">
                            Entrar no site
                          </a>
                        </td>
                      </tr>
                    </table>
                    <p style="font-size: 13px; color: #666; text-align: center; margin-top: 16px;">
                      Seus créditos já estão disponíveis em <a href="{siteUrl}" style="color: #4f46e5;">{frontendUrl}</a>.
                    </p>
                  </div>
                </body></html>
                """;

            var text = $"{greeting} Compra confirmada. Plano: {details.PlanName}. Valor: R$ {priceStr}. Data: {now}. Entrar no site: {siteUrl}";

            await SendAsync(
                clientEmail,
                $"✅ Confirmação de compra - {AppNameBranded}",
                html,
                text,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enviar confirmação de compra");
        }
    }

    public Task SendTestEmailAsync(string to, CancellationToken cancellationToken = default)
    {
        var smtpHost = _configuration["SMTP_HOST"]?.Trim() ?? _configuration["EMAIL_HOST"]?.Trim() ?? "(não definido)";
        var smtpAlt = _configuration["SMTP_HOST_ALTERNATIVE"]?.Trim()
            ?? _configuration["SMTP_HOST_ALTERNATIVO"]?.Trim()
            ?? "(não definido)";
        var sender = GetEmailUser() ?? "(não definido)";
        var bcc = GetDefaultBcc();
        var now = FormatBrazilDateTimeDetailed();

        var html = $"""
            <!DOCTYPE html>
            <html><head><meta charset="utf-8"></head>
            <body style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;">
              <div style="background: #f9f9f9; border-radius: 8px; padding: 30px;">
                <h2 style="color: #4CAF50;">✅ Teste SMTP — {AppNameBranded}</h2>
                <p>Este é um e-mail de diagnóstico enviado pelo endpoint <code>GET /api/test/email</code>.</p>
                <ul>
                  <li><strong>Data/hora:</strong> {now}</li>
                  <li><strong>Remetente:</strong> {sender}</li>
                  <li><strong>SMTP primário:</strong> {smtpHost}</li>
                  <li><strong>SMTP alternativo:</strong> {smtpAlt}</li>
                  <li><strong>BCC padrão:</strong> {bcc}</li>
                </ul>
                <p>Se você recebeu esta mensagem, o disparo de e-mail está funcionando.</p>
              </div>
            </body></html>
            """;

        var text = $"Teste SMTP {AppNameBranded} — {now}. Remetente: {sender}. SMTP: {smtpHost}.";

        return SendAsync(
            to,
            $"🧪 Teste SMTP — {AppNameBranded}",
            html,
            text,
            cancellationToken: cancellationToken);
    }

    private async Task SendAsync(
        string to,
        string subject,
        string htmlBody,
        string textBody,
        bool useCc = false,
        string? bcc = null,
        CancellationToken cancellationToken = default)
    {
        var sender = GetEmailUser();
        var password = GetEmailPassword();

        if (string.IsNullOrEmpty(sender) || string.IsNullOrEmpty(password))
        {
            _logger.LogError(
                "Email não configurado. EMAIL_SENDER: {Sender}, senha: {Password}",
                string.IsNullOrEmpty(sender) ? "ausente" : "ok",
                string.IsNullOrEmpty(password) ? "ausente" : "ok");
            throw new InvalidOperationException(
                "Serviço de email não configurado. Verifique as variáveis de ambiente no arquivo .env");
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(GetSenderDisplayName(), sender));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;

        if (useCc)
        {
            var cc = _configuration["EMAIL_COPY"]?.Trim() ?? _configuration["EMAIL_COPY_TO"]?.Trim();
            if (!string.IsNullOrEmpty(cc))
            {
                message.Cc.Add(MailboxAddress.Parse(cc));
            }
        }

        if (!string.IsNullOrEmpty(bcc))
        {
            message.Bcc.Add(MailboxAddress.Parse(bcc));
        }

        var defaultBcc = GetDefaultBcc();
        if (!string.IsNullOrEmpty(defaultBcc)
            && !message.Bcc.Any(x => string.Equals(x.ToString(), defaultBcc, StringComparison.OrdinalIgnoreCase)))
        {
            message.Bcc.Add(MailboxAddress.Parse(defaultBcc));
        }

        var body = new BodyBuilder { HtmlBody = htmlBody, TextBody = textBody };
        message.Body = body.ToMessageBody();

        using var client = new SmtpClient();
        await ConnectSmtpAsync(client, cancellationToken);

        await client.AuthenticateAsync(sender, password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);

        _logger.LogInformation("Email enviado para {To}: {Subject}", to, subject);
    }

    private async Task ConnectSmtpAsync(SmtpClient client, CancellationToken cancellationToken)
    {
        if (string.Equals(_configuration["EMAIL_SERVICE"], "gmail", StringComparison.OrdinalIgnoreCase))
        {
            await client.ConnectAsync("smtp.gmail.com", 587, SecureSocketOptions.StartTls, cancellationToken);
            return;
        }

        var (_, port, secure) = GetSmtpSettings();
        if (port <= 0)
        {
            throw new InvalidOperationException("Configuração de email incompleta (SMTP_PORT).");
        }

        var hosts = GetSmtpHosts().ToList();
        if (hosts.Count == 0)
        {
            throw new InvalidOperationException("Configuração de email incompleta (SMTP_HOST).");
        }

        var socketOptions = secure || port == 465
            ? SecureSocketOptions.SslOnConnect
            : port == 587
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.Auto;

        Exception? lastError = null;
        foreach (var host in hosts)
        {
            try
            {
                if (client.IsConnected)
                {
                    await client.DisconnectAsync(true, cancellationToken);
                }

                await client.ConnectAsync(host, port, socketOptions, cancellationToken);
                _logger.LogDebug("SMTP conectado em {Host}:{Port}", host, port);
                return;
            }
            catch (Exception ex) when (ex is System.Net.Sockets.SocketException or SmtpCommandException or SmtpProtocolException)
            {
                lastError = ex;
                _logger.LogWarning(ex, "Falha ao conectar SMTP em {Host}:{Port}", host, port);
            }
        }

        throw new InvalidOperationException(
            $"Não foi possível conectar ao servidor SMTP ({string.Join(", ", hosts)}).",
            lastError);
    }

    private IEnumerable<string> GetSmtpHosts()
    {
        var primary = _configuration["SMTP_HOST"]?.Trim() ?? _configuration["EMAIL_HOST"]?.Trim();
        var alternative = _configuration["SMTP_HOST_ALTERNATIVE"]?.Trim()
            ?? _configuration["SMTP_HOST_ALTERNATIVO"]?.Trim();

        if (!string.IsNullOrEmpty(primary))
        {
            yield return primary;
        }

        if (!string.IsNullOrEmpty(alternative)
            && !string.Equals(alternative, primary, StringComparison.OrdinalIgnoreCase))
        {
            yield return alternative;
        }
    }

    private string GetDefaultBcc() =>
        _configuration["EMAIL_BCC_TO"]?.Trim()
        ?? _configuration["EMAIL_COPY_BCC"]?.Trim()
        ?? "juniorbx@gmail.com";

    private (string? Host, int Port, bool Secure) GetSmtpSettings()
    {
        var host = _configuration["EMAIL_HOST"]?.Trim() ?? _configuration["SMTP_HOST"]?.Trim();
        var portStr = _configuration["EMAIL_PORT"]?.Trim() ?? _configuration["SMTP_PORT"]?.Trim();
        var secure = string.Equals(_configuration["EMAIL_SECURE"], "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(_configuration["SMTP_SECURE"], "true", StringComparison.OrdinalIgnoreCase);
        _ = int.TryParse(portStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port);
        return (host, port, secure);
    }

    private string? GetEmailUser() =>
        _configuration["EMAIL_USER"]?.Trim() ?? _configuration["EMAIL_SENDER"]?.Trim();

    private string? GetEmailPassword() =>
        _configuration["EMAIL_PASSWORD"]?.Trim() ?? _configuration["EMAIL_SENDER_PASSWORD"]?.Trim();

    private string GetSenderDisplayName() =>
        _configuration["EMAIL_SENDER_NAME"]?.Trim() ?? AppNameVerification;

    private string GetFrontendUrl() =>
        _configuration["FRONTEND_URL"]?.Trim() ?? "http://localhost:4200";

    private static string FormatGreeting(string name) =>
        string.IsNullOrWhiteSpace(name) ? "Olá!" : $"Olá, {name}!";

    private static string FormatBrazilDateTime() =>
        TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById(
                OperatingSystem.IsWindows() ? "E. South America Standard Time" : "America/Sao_Paulo"))
            .ToString("g", new CultureInfo("pt-BR"));

    private static string FormatBrazilDateTimeDetailed() =>
        TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById(
                OperatingSystem.IsWindows() ? "E. South America Standard Time" : "America/Sao_Paulo"))
            .ToString("dd/MM/yyyy HH:mm", new CultureInfo("pt-BR"));

    private static string FormatPrice(object? price) => price switch
    {
        null => "0,00",
        decimal d => d.ToString("F2", new CultureInfo("pt-BR")),
        double db => db.ToString("F2", new CultureInfo("pt-BR")),
        float f => f.ToString("F2", new CultureInfo("pt-BR")),
        int i => i.ToString("F2", new CultureInfo("pt-BR")),
        _ => price.ToString()?.Replace('.', ',') ?? "0,00"
    };
}
