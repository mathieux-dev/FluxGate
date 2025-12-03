using System.Text.Json;
using FluxPay.Core.Entities;
using FluxPay.Core.Services;
using FluxPay.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FluxPay.Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly FluxPayDbContext _dbContext;
    private readonly IHmacSignatureService _hmacService;
    private readonly string _auditHmacKey;

    public AuditService(
        FluxPayDbContext dbContext,
        IHmacSignatureService hmacService)
    {
        _dbContext = dbContext;
        _hmacService = hmacService;
        
        _auditHmacKey = Environment.GetEnvironmentVariable("AUDIT_HMAC_KEY") 
            ?? throw new InvalidOperationException("AUDIT_HMAC_KEY environment variable is not set");
    }

    public async Task LogAsync(AuditEntry entry)
    {
        if (entry == null)
        {
            throw new ArgumentNullException(nameof(entry));
        }

        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            MerchantId = entry.MerchantId,
            Actor = entry.Actor,
            Action = entry.Action,
            ResourceType = entry.ResourceType,
            ResourceId = entry.ResourceId,
            Changes = entry.Changes != null ? JsonSerializer.Serialize(entry.Changes) : null,
            CreatedAt = DateTime.UtcNow
        };

        var message = BuildSignatureMessage(auditLog);
        auditLog.Signature = _hmacService.ComputeSignature(_auditHmacKey, message);

        _dbContext.AuditLogs.Add(auditLog);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<bool> VerifyIntegrityAsync(Guid auditId)
    {
        var auditLog = await _dbContext.AuditLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == auditId);

        if (auditLog == null)
        {
            return false;
        }

        var message = BuildSignatureMessage(auditLog);
        return _hmacService.VerifySignature(_auditHmacKey, message, auditLog.Signature);
    }

    public async Task ExportToStorageAsync(DateTime startDate, DateTime endDate)
    {
        var auditLogs = await _dbContext.AuditLogs
            .AsNoTracking()
            .Where(a => a.CreatedAt >= startDate && a.CreatedAt <= endDate)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync();

        var exportData = auditLogs.Select(log => new
        {
            log.Id,
            log.MerchantId,
            log.Actor,
            log.Action,
            log.ResourceType,
            log.ResourceId,
            log.Changes,
            log.Signature,
            log.CreatedAt
        });

        var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        var fileName = $"audit_logs_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.json";
        var exportPath = Path.Combine(Directory.GetCurrentDirectory(), "exports", fileName);
        
        Directory.CreateDirectory(Path.GetDirectoryName(exportPath)!);
        await File.WriteAllTextAsync(exportPath, json);
    }

    private string BuildSignatureMessage(AuditLog auditLog)
    {
        return $"{auditLog.Id}|{auditLog.MerchantId}|{auditLog.Actor}|{auditLog.Action}|{auditLog.ResourceType}|{auditLog.ResourceId}|{auditLog.Changes}|{auditLog.CreatedAt:O}";
    }
}
