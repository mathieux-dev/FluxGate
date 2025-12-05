namespace FluxPay.Core.Entities;

public class Payment
{
    public Guid Id { get; set; }
    public Guid MerchantId { get; set; }
    public Guid? CustomerId { get; set; }
    public long AmountCents { get; set; }
    public PaymentMethod Method { get; set; }
    public PaymentStatus Status { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string? ProviderPaymentId { get; set; }
    public string? ProviderPayload { get; set; }
    public string? Metadata { get; set; }
    public bool IsTest { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    public Merchant Merchant { get; set; } = null!;
    public Customer? Customer { get; set; }
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<WebhookDelivery> WebhookDeliveries { get; set; } = new List<WebhookDelivery>();
}

public enum PaymentMethod
{
    CreditCard,
    DebitCard,
    Pix,
    Boleto
}

public enum PaymentStatus
{
    Pending,
    Authorized,
    Paid,
    Refunded,
    Failed,
    Expired,
    Cancelled
}
