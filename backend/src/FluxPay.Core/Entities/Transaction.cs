namespace FluxPay.Core.Entities;

public class Transaction
{
    public Guid Id { get; set; }
    public Guid PaymentId { get; set; }
    public TransactionType Type { get; set; }
    public TransactionStatus Status { get; set; }
    public long AmountCents { get; set; }
    public string? ProviderTxId { get; set; }
    public string? Payload { get; set; }
    public bool IsTest { get; set; }
    public DateTime CreatedAt { get; set; }
    
    public Payment Payment { get; set; } = null!;
}

public enum TransactionType
{
    Authorization,
    Capture,
    Refund,
    Chargeback
}

public enum TransactionStatus
{
    Pending,
    Success,
    Failed
}
