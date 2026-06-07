namespace CurriculosProIA.Service.Helpers;

/// <summary>URLs de retorno do checkout no frontend (Mercado Pago / Stripe).</summary>
public static class PaymentReturnUrls
{
    public const string SuccessPath = "compra/sucesso";
    public const string PendingPath = "compra/pendente";
    public const string FailurePath = "compra/falha";
    public const string CancelledPath = "compra/cancelada";

    public static string Build(
        string frontendBaseUrl,
        string path,
        string provider,
        string userId,
        string? analysisId = null,
        bool englishPaid = false,
        bool freeCheckout = false)
    {
        var baseUrl = frontendBaseUrl.TrimEnd('/');
        var query = new List<string>();

        if (freeCheckout)
        {
            query.Add("free=1");
        }
        else
        {
            query.Add($"provider={Uri.EscapeDataString(provider)}");
        }

        query.Add($"userId={Uri.EscapeDataString(userId)}");

        if (!string.IsNullOrWhiteSpace(analysisId))
        {
            query.Add($"analysisId={Uri.EscapeDataString(analysisId.Trim())}");
        }

        if (englishPaid)
        {
            query.Add("englishPaid=1");
        }

        return $"{baseUrl}/{path.TrimStart('/')}?{string.Join("&", query)}";
    }

    /// <summary>
    /// Mercado Pago exige HTTPS e proíbe localhost/127.0.0.1 para auto_return e notification_url.
    /// </summary>
    public static bool SupportsMercadoPagoHttpsCallback(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !url.Contains("localhost", StringComparison.OrdinalIgnoreCase)
            && !url.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase);
    }
}
