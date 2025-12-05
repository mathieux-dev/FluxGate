using FsCheck;
using FsCheck.Xunit;
using FluxPay.Core.Services;
using FluxPay.Infrastructure.Data;
using FluxPay.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace FluxPay.Tests.Unit.Properties;

public class AuditServicePropertyTests : IDisposable
{
    private readonly string _originalEncryptionKey;
    private readonly string _originalAuditKey;
    private readonly FluxPayDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly IHmacSignatureService _hmacService;

    public AuditServicePropertyTests()
    {
        _originalEncryptionKey = Environment.GetEnvironmentVariable("MASTER_ENCRYPTION_KEY") ?? string.Empty;
        _originalAuditKey = Environment.GetEnvironmentVariable("AUDIT_HMAC_KEY") ?? string.Empty;
        
        var encryptionKey = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(encryptionKey);
        Environment.SetEnvironmentVariable("MASTER_ENCRYPTION_KEY", Convert.ToBase64String(encryptionKey));
        
        var auditKey = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(auditKey);
        Environment.SetEnvironmentVariable("AUDIT_HMAC_KEY", Convert.ToBase64String(auditKey));

        var options = new DbContextOptionsBuilder<FluxPayDbContext>()
            .UseInMemoryDatabase(databaseName: $"AuditTest_{Guid.NewGuid()}")
            .EnableSensitiveDataLogging()
            .Options;
        
        _dbContext = new FluxPayDbContext(options);
        _dbContext.Database.EnsureCreated();
        _hmacService = new HmacSignatureService();
        _auditService = new AuditService(_dbContext, _hmacService);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
        
        if (string.IsNullOrEmpty(_originalEncryptionKey))
        {
            Environment.SetEnvironmentVariable("MASTER_ENCRYPTION_KEY", null);
        }
        else
        {
            Environment.SetEnvironmentVariable("MASTER_ENCRYPTION_KEY", _originalEncryptionKey);
        }
        
        if (string.IsNullOrEmpty(_originalAuditKey))
        {
            Environment.SetEnvironmentVariable("AUDIT_HMAC_KEY", null);
        }
        else
        {
            Environment.SetEnvironmentVariable("AUDIT_HMAC_KEY", _originalAuditKey);
        }
    }

    [Property(MaxTest = 10)]
    public void Payment_Operation_Audit_Logging_Should_Create_Entry_With_All_Required_Fields(
        NonEmptyString actor,
        NonEmptyString action,
        NonEmptyString resourceType)
    {
        Prop.ForAll(
            Arb.From(Gen.Elements(actor.Get)),
            Arb.From(Gen.Elements(action.Get)),
            Arb.From(Gen.Elements(resourceType.Get)),
            (actorValue, actionValue, resourceTypeValue) =>
            {
                var merchantId = Guid.NewGuid();
                var resourceId = Guid.NewGuid();
                var changes = new { Status = "paid", Amount = 10000 };

                var entry = new AuditEntry
                {
                    MerchantId = merchantId,
                    Actor = actorValue,
                    Action = actionValue,
                    ResourceType = resourceTypeValue,
                    ResourceId = resourceId,
                    Changes = changes
                };

                _auditService.LogAsync(entry).Wait();

                var auditLog = _dbContext.AuditLogs
                    .FirstOrDefaultAsync(a => a.MerchantId == merchantId && a.ResourceId == resourceId).Result;

                return auditLog != null
                    && auditLog.MerchantId == merchantId
                    && auditLog.Actor == actorValue
                    && auditLog.Action == actionValue
                    && auditLog.ResourceType == resourceTypeValue
                    && auditLog.ResourceId == resourceId
                    && !string.IsNullOrEmpty(auditLog.Changes)
                    && !string.IsNullOrEmpty(auditLog.Signature)
                    && auditLog.CreatedAt != default;
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 10)]
    public void Audit_Log_Signature_Integrity_Should_Verify_Successfully_After_Creation(
        NonEmptyString actor,
        NonEmptyString action)
    {
        Prop.ForAll(
            Arb.From(Gen.Elements(actor.Get)),
            Arb.From(Gen.Elements(action.Get)),
            (actorValue, actionValue) =>
            {
                var entry = new AuditEntry
                {
                    MerchantId = Guid.NewGuid(),
                    Actor = actorValue,
                    Action = actionValue,
                    ResourceType = "Payment",
                    ResourceId = Guid.NewGuid(),
                    Changes = new { Status = "paid" }
                };

                _auditService.LogAsync(entry).Wait();

                var auditLog = _dbContext.AuditLogs
                    .FirstOrDefaultAsync(a => a.Actor == actorValue && a.Action == actionValue).Result;

                if (auditLog == null)
                {
                    return false;
                }

                var isValid = _auditService.VerifyIntegrityAsync(auditLog.Id).Result;
                return isValid;
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 10)]
    public void Audit_Log_Should_Detect_Tampering_When_Signature_Is_Modified(
        NonEmptyString actor,
        NonEmptyString action)
    {
        Prop.ForAll(
            Arb.From(Gen.Elements(actor.Get)),
            Arb.From(Gen.Elements(action.Get)),
            (actorValue, actionValue) =>
            {
                var entry = new AuditEntry
                {
                    MerchantId = Guid.NewGuid(),
                    Actor = actorValue,
                    Action = actionValue,
                    ResourceType = "Payment",
                    ResourceId = Guid.NewGuid(),
                    Changes = new { Status = "paid" }
                };

                _auditService.LogAsync(entry).Wait();

                var auditLog = _dbContext.AuditLogs
                    .FirstOrDefaultAsync(a => a.Actor == actorValue && a.Action == actionValue).Result;

                if (auditLog == null)
                {
                    return false;
                }

                auditLog.Signature = "tampered_signature_" + auditLog.Signature;
                _dbContext.SaveChangesAsync().Wait();

                var isValid = _auditService.VerifyIntegrityAsync(auditLog.Id).Result;
                return !isValid;
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 10)]
    public void Audit_Log_Should_Detect_Tampering_When_Data_Is_Modified(
        NonEmptyString actor,
        NonEmptyString action,
        NonEmptyString tamperedAction)
    {
        Prop.ForAll(
            Arb.From(Gen.Elements(actor.Get)),
            Arb.From(Gen.Elements(action.Get)),
            Arb.From(Gen.Elements(tamperedAction.Get)),
            (actorValue, actionValue, tamperedActionValue) =>
            {
                if (actionValue == tamperedActionValue)
                {
                    return true;
                }

                var entry = new AuditEntry
                {
                    MerchantId = Guid.NewGuid(),
                    Actor = actorValue,
                    Action = actionValue,
                    ResourceType = "Payment",
                    ResourceId = Guid.NewGuid(),
                    Changes = new { Status = "paid" }
                };

                _auditService.LogAsync(entry).Wait();

                var auditLog = _dbContext.AuditLogs
                    .FirstOrDefaultAsync(a => a.Actor == actorValue && a.Action == actionValue).Result;

                if (auditLog == null)
                {
                    return false;
                }

                auditLog.Action = tamperedActionValue;
                _dbContext.SaveChangesAsync().Wait();

                var isValid = _auditService.VerifyIntegrityAsync(auditLog.Id).Result;
                return !isValid;
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 10)]
    public void Multiple_Audit_Logs_Should_Have_Unique_Signatures(
        NonEmptyString actor1,
        NonEmptyString actor2)
    {
        Prop.ForAll(
            Arb.From(Gen.Elements(actor1.Get)),
            Arb.From(Gen.Elements(actor2.Get)),
            (actorValue1, actorValue2) =>
            {
                var entry1 = new AuditEntry
                {
                    MerchantId = Guid.NewGuid(),
                    Actor = actorValue1,
                    Action = "create_payment",
                    ResourceType = "Payment",
                    ResourceId = Guid.NewGuid(),
                    Changes = new { Status = "pending" }
                };

                var entry2 = new AuditEntry
                {
                    MerchantId = Guid.NewGuid(),
                    Actor = actorValue2,
                    Action = "update_payment",
                    ResourceType = "Payment",
                    ResourceId = Guid.NewGuid(),
                    Changes = new { Status = "paid" }
                };

                _auditService.LogAsync(entry1).Wait();
                _auditService.LogAsync(entry2).Wait();

                var auditLog1 = _dbContext.AuditLogs
                    .FirstOrDefaultAsync(a => a.Actor == actorValue1).Result;
                var auditLog2 = _dbContext.AuditLogs
                    .FirstOrDefaultAsync(a => a.Actor == actorValue2).Result;

                return auditLog1 != null
                    && auditLog2 != null
                    && auditLog1.Signature != auditLog2.Signature;
            }
        ).QuickCheckThrowOnFailure();
    }
}
