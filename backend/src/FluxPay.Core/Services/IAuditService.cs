namespace FluxPay.Core.Services;

public interface IAuditService
{
    Task LogAsync(AuditEntry entry);
    Task<bool> VerifyIntegrityAsync(Guid auditId);
    Task ExportToStorageAsync(DateTime startDate, DateTime endDate);
}

public class AuditEntry
{
    public Guid? MerchantId { get; set; }
    public string Actor { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public Guid? ResourceId { get; set; }
    public object? Changes { get; set; }
}
