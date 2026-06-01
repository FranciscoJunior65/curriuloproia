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
}
