using System.Text.Json.Serialization;

namespace CurriculosProIA.Domain.Signatures.Analyze;

public class KiwifyWebhookSignature
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("signature")]
    public string? Signature { get; set; }

    [JsonPropertyName("token")]
    public string? Token { get; set; }

    [JsonPropertyName("order")]
    public KiwifyWebhookOrderSignature? Order { get; set; }
}

public class KiwifyWebhookOrderSignature
{
    [JsonPropertyName("order_id")]
    public string? OrderId { get; set; }

    [JsonPropertyName("order_ref")]
    public string? OrderRef { get; set; }

    [JsonPropertyName("order_status")]
    public string? OrderStatus { get; set; }

    [JsonPropertyName("product_type")]
    public string? ProductType { get; set; }

    [JsonPropertyName("payment_method")]
    public string? PaymentMethod { get; set; }

    [JsonPropertyName("store_id")]
    public string? StoreId { get; set; }

    [JsonPropertyName("payment_merchant_id")]
    public string? PaymentMerchantId { get; set; }

    [JsonPropertyName("boleto_barcode")]
    public string? BoletoBarcode { get; set; }

    [JsonPropertyName("boleto_expiry_date")]
    public string? BoletoExpiryDate { get; set; }

    [JsonPropertyName("pix_code")]
    public string? PixCode { get; set; }

    [JsonPropertyName("pix_expiration")]
    public string? PixExpiration { get; set; }

    [JsonPropertyName("sale_type")]
    public string? SaleType { get; set; }

    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public string? UpdatedAt { get; set; }

    [JsonPropertyName("approved_date")]
    public string? ApprovedDate { get; set; }

    [JsonPropertyName("refunded_at")]
    public string? RefundedAt { get; set; }

    [JsonPropertyName("webhook_event_type")]
    public string? WebhookEventType { get; set; }

    [JsonPropertyName("Product")]
    public KiwifyWebhookProductSignature? Product { get; set; }

    [JsonPropertyName("Customer")]
    public KiwifyWebhookCustomerSignature? Customer { get; set; }

    [JsonPropertyName("Commissions")]
    public KiwifyWebhookCommissionsSignature? Commissions { get; set; }

    [JsonPropertyName("TrackingParameters")]
    public KiwifyWebhookTrackingParametersSignature? TrackingParameters { get; set; }

    [JsonPropertyName("checkout_link")]
    public string? CheckoutLink { get; set; }

    [JsonPropertyName("access_url")]
    public string? AccessUrl { get; set; }
}

public class KiwifyWebhookProductSignature
{
    [JsonPropertyName("product_id")]
    public string? ProductId { get; set; }

    [JsonPropertyName("product_name")]
    public string? ProductName { get; set; }

    [JsonPropertyName("product_offer_id")]
    public string? ProductOfferId { get; set; }

    [JsonPropertyName("product_offer_name")]
    public string? ProductOfferName { get; set; }
}

public class KiwifyWebhookCustomerSignature
{
    [JsonPropertyName("full_name")]
    public string? FullName { get; set; }

    [JsonPropertyName("first_name")]
    public string? FirstName { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("mobile")]
    public string? Mobile { get; set; }

    [JsonPropertyName("CPF")]
    public string? Cpf { get; set; }

    [JsonPropertyName("cnpj")]
    public string? Cnpj { get; set; }

    [JsonPropertyName("ip")]
    public string? Ip { get; set; }

    [JsonPropertyName("instagram")]
    public string? Instagram { get; set; }

    [JsonPropertyName("country")]
    public string? Country { get; set; }

    [JsonPropertyName("custom_fields")]
    public List<object> CustomFields { get; set; } = new();
}

public class KiwifyWebhookCommissionsSignature
{
    [JsonPropertyName("charge_amount")]
    public decimal? ChargeAmount { get; set; }

    [JsonPropertyName("product_base_price")]
    public decimal? ProductBasePrice { get; set; }

    [JsonPropertyName("product_base_price_currency")]
    public string? ProductBasePriceCurrency { get; set; }

    [JsonPropertyName("kiwify_fee")]
    public decimal? KiwifyFee { get; set; }

    [JsonPropertyName("kiwify_fee_currency")]
    public string? KiwifyFeeCurrency { get; set; }

    [JsonPropertyName("commissioned_stores")]
    public List<KiwifyWebhookCommissionedStoreSignature> CommissionedStores { get; set; } = new();

    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    [JsonPropertyName("my_commission")]
    public decimal? MyCommission { get; set; }

    [JsonPropertyName("funds_status")]
    public string? FundsStatus { get; set; }

    [JsonPropertyName("estimated_deposit_date")]
    public string? EstimatedDepositDate { get; set; }

    [JsonPropertyName("deposit_date")]
    public string? DepositDate { get; set; }
}

public class KiwifyWebhookCommissionedStoreSignature
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("custom_name")]
    public string? CustomName { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }
}

public class KiwifyWebhookTrackingParametersSignature
{
    [JsonPropertyName("src")]
    public string? Src { get; set; }

    [JsonPropertyName("sck")]
    public string? Sck { get; set; }

    [JsonPropertyName("utm_source")]
    public string? UtmSource { get; set; }

    [JsonPropertyName("utm_medium")]
    public string? UtmMedium { get; set; }

    [JsonPropertyName("utm_campaign")]
    public string? UtmCampaign { get; set; }

    [JsonPropertyName("utm_content")]
    public string? UtmContent { get; set; }

    [JsonPropertyName("utm_term")]
    public string? UtmTerm { get; set; }

    [JsonPropertyName("s1")]
    public string? S1 { get; set; }

    [JsonPropertyName("s2")]
    public string? S2 { get; set; }

    [JsonPropertyName("s3")]
    public string? S3 { get; set; }
}
