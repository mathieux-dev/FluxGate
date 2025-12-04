namespace FluxPay.Core.Services;

public interface IWebhookService
{
    Task<bool> ValidateProviderWebhookAsync(string provider, string signature, string payload, long timestamp, string nonce);
    Task ProcessProviderWebhookAsync(ProviderWebhookEvent webhookEvent);
    Task SendMerchantWebhookAsync(Guid merchantId, WebhookEvent webhookEvent);
    Task<WebhookDeliveryResult> TestMerchantWebhookAsync(Guid merchantId, string endpointUrl);
}

public class ProviderWebhookEvent
{
    public string Provider { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string? ProviderPaymentId { get; set; }
    public string? Status { get; set; }
}

public class WebhookEvent
{
    public string EventType { get; set; } = string.Empty;
    public Guid PaymentId { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
    public long AmountCents { get; set; }
    public string Method { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}

public class WebhookDeliveryResult
{
    public bool Success { get; set; }
    public int StatusCode { get; set; }
    public long ResponseTimeMs { get; set; }
    public string? ResponseBody { get; set; }
    public string? ErrorMessage { get; set; }
}
