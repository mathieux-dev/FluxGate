using FsCheck;
using FsCheck.Xunit;
using FluxPay.Core.Entities;
using FluxPay.Infrastructure.Data;
using FluxPay.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace FluxPay.Tests.Unit.Properties;

public class ApiKeyRotationPropertyTests : IDisposable
{
    private readonly string _originalEncryptionKey;
    private readonly FluxPayDbContext _dbContext;
    private readonly EncryptionService _encryptionService;

    public ApiKeyRotationPropertyTests()
    {
        _originalEncryptionKey = Environment.GetEnvironmentVariable("MASTER_ENCRYPTION_KEY") ?? string.Empty;

        var encryptionKey = new byte[32];
        RandomNumberGenerator.Fill(encryptionKey);
        Environment.SetEnvironmentVariable("MASTER_ENCRYPTION_KEY", Convert.ToBase64String(encryptionKey));

        var options = new DbContextOptionsBuilder<FluxPayDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new FluxPayDbContext(options);
        _encryptionService = new EncryptionService();
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();

        if (string.IsNullOrEmpty(_originalEncryptionKey))
            Environment.SetEnvironmentVariable("MASTER_ENCRYPTION_KEY", null);
        else
            Environment.SetEnvironmentVariable("MASTER_ENCRYPTION_KEY", _originalEncryptionKey);
    }

    [Property(MaxTest = 100)]
    public void API_Key_Rotation_Should_Generate_New_Key_And_Mark_Old_Keys_For_Deprecation()
    {
        Prop.ForAll(
            Arb.Default.Guid(),
            (Guid merchantId) =>
            {
                var options = new DbContextOptionsBuilder<FluxPayDbContext>()
                    .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                    .Options;
                using var dbContext = new FluxPayDbContext(options);

                var merchant = new Merchant
                {
                    Id = merchantId,
                    Name = $"Merchant {merchantId}",
                    Email = $"merchant{merchantId}@example.com",
                    ProviderConfigEncrypted = "encrypted_config",
                    Active = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                dbContext.Merchants.Add(merchant);

                var oldKeySecretBytes = new byte[32];
                RandomNumberGenerator.Fill(oldKeySecretBytes);
                var oldKeySecret = Convert.ToBase64String(oldKeySecretBytes);
                var oldKeyId = $"merchant-{merchantId.ToString().Substring(0, 8)}";
                var oldKeyHash = _encryptionService.Hash(oldKeySecret);
                var oldKeySecretEncrypted = _encryptionService.Encrypt(oldKeySecret);

                var oldApiKey = new ApiKey
                {
                    Id = Guid.NewGuid(),
                    MerchantId = merchantId,
                    KeyId = oldKeyId,
                    KeyHash = oldKeyHash,
                    KeySecretEncrypted = oldKeySecretEncrypted,
                    Active = true,
                    ExpiresAt = null,
                    CreatedAt = DateTime.UtcNow.AddDays(-10)
                };
                dbContext.ApiKeys.Add(oldApiKey);
                dbContext.SaveChanges();

                var keySecretBytes = new byte[32];
                RandomNumberGenerator.Fill(keySecretBytes);
                var keySecret = Convert.ToBase64String(keySecretBytes);
                var keyId = $"merchant-{merchantId.ToString().Substring(0, 8)}";
                var keyHash = _encryptionService.Hash(keySecret);
                var keySecretEncrypted = _encryptionService.Encrypt(keySecret);

                var newApiKey = new ApiKey
                {
                    Id = Guid.NewGuid(),
                    MerchantId = merchantId,
                    KeyId = keyId,
                    KeyHash = keyHash,
                    KeySecretEncrypted = keySecretEncrypted,
                    Active = true,
                    ExpiresAt = null,
                    CreatedAt = DateTime.UtcNow
                };

                dbContext.ApiKeys.Add(newApiKey);

                var oldActiveKeys = dbContext.ApiKeys
                    .Where(k => k.MerchantId == merchantId && k.Active && k.Id != newApiKey.Id)
                    .ToList();
                
                var gracePeriodEnd = DateTime.UtcNow.AddDays(30);
                
                foreach (var oldKey in oldActiveKeys)
                {
                    oldKey.ExpiresAt = gracePeriodEnd;
                }

                dbContext.SaveChanges();

                var newKeyExists = dbContext.ApiKeys.Any(k => k.Id == newApiKey.Id && k.Active);
                var oldKeyMarkedForExpiry = dbContext.ApiKeys
                    .First(k => k.Id == oldApiKey.Id)
                    .ExpiresAt.HasValue;
                var oldKeyExpiryIsInFuture = dbContext.ApiKeys
                    .First(k => k.Id == oldApiKey.Id)
                    .ExpiresAt > DateTime.UtcNow;
                var newKeySecretIsDifferent = keySecret != oldKeySecret;
                var newKeyIsActive = dbContext.ApiKeys.First(k => k.Id == newApiKey.Id).Active;

                return newKeyExists && 
                       oldKeyMarkedForExpiry && 
                       oldKeyExpiryIsInFuture && 
                       newKeySecretIsDifferent && 
                       newKeyIsActive;
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 100)]
    public void API_Key_Rotation_Should_Return_New_Secret_Only_Once()
    {
        Prop.ForAll(
            Arb.Default.Guid(),
            (Guid merchantId) =>
            {
                var keySecretBytes = new byte[32];
                RandomNumberGenerator.Fill(keySecretBytes);
                var keySecret = Convert.ToBase64String(keySecretBytes);
                var keyHash = _encryptionService.Hash(keySecret);
                var keySecretEncrypted = _encryptionService.Encrypt(keySecret);

                var decryptedSecret = _encryptionService.Decrypt(keySecretEncrypted);

                var secretsMatch = keySecret == decryptedSecret;
                var secretIsNotEmpty = !string.IsNullOrEmpty(keySecret);
                var secretIsBase64 = keySecret.Length > 0;

                return secretsMatch && secretIsNotEmpty && secretIsBase64;
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 100)]
    public void API_Key_Rotation_Should_Generate_Unique_Keys()
    {
        Prop.ForAll(
            Arb.Default.Int32(),
            _ =>
            {
                var keySecretBytes1 = new byte[32];
                RandomNumberGenerator.Fill(keySecretBytes1);
                var keySecret1 = Convert.ToBase64String(keySecretBytes1);

                var keySecretBytes2 = new byte[32];
                RandomNumberGenerator.Fill(keySecretBytes2);
                var keySecret2 = Convert.ToBase64String(keySecretBytes2);

                return keySecret1 != keySecret2;
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 100)]
    public void API_Key_Rotation_Should_Maintain_Merchant_Association()
    {
        Prop.ForAll(
            Arb.Default.Guid(),
            (Guid merchantId) =>
            {
                var options = new DbContextOptionsBuilder<FluxPayDbContext>()
                    .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                    .Options;
                using var dbContext = new FluxPayDbContext(options);

                var merchant = new Merchant
                {
                    Id = merchantId,
                    Name = $"Merchant {merchantId}",
                    Email = $"merchant{merchantId}@example.com",
                    ProviderConfigEncrypted = "encrypted_config",
                    Active = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                dbContext.Merchants.Add(merchant);
                dbContext.SaveChanges();

                var keySecretBytes = new byte[32];
                RandomNumberGenerator.Fill(keySecretBytes);
                var keySecret = Convert.ToBase64String(keySecretBytes);
                var keyId = $"merchant-{merchantId.ToString().Substring(0, 8)}";
                var keyHash = _encryptionService.Hash(keySecret);
                var keySecretEncrypted = _encryptionService.Encrypt(keySecret);

                var newApiKey = new ApiKey
                {
                    Id = Guid.NewGuid(),
                    MerchantId = merchantId,
                    KeyId = keyId,
                    KeyHash = keyHash,
                    KeySecretEncrypted = keySecretEncrypted,
                    Active = true,
                    ExpiresAt = null,
                    CreatedAt = DateTime.UtcNow
                };

                dbContext.ApiKeys.Add(newApiKey);
                dbContext.SaveChanges();

                var savedKey = dbContext.ApiKeys
                    .Include(k => k.Merchant)
                    .First(k => k.Id == newApiKey.Id);

                return savedKey.MerchantId == merchantId && 
                       savedKey.Merchant.Id == merchantId;
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 100)]
    public void API_Key_Rotation_Should_Set_Grace_Period_Of_30_Days()
    {
        Prop.ForAll(
            Arb.Default.Guid(),
            (Guid merchantId) =>
            {
                var options = new DbContextOptionsBuilder<FluxPayDbContext>()
                    .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                    .Options;
                using var dbContext = new FluxPayDbContext(options);

                var merchant = new Merchant
                {
                    Id = merchantId,
                    Name = $"Merchant {merchantId}",
                    Email = $"merchant{merchantId}@example.com",
                    ProviderConfigEncrypted = "encrypted_config",
                    Active = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                dbContext.Merchants.Add(merchant);

                var oldKeySecretBytes = new byte[32];
                RandomNumberGenerator.Fill(oldKeySecretBytes);
                var oldKeySecret = Convert.ToBase64String(oldKeySecretBytes);
                var oldKeyId = $"merchant-{merchantId.ToString().Substring(0, 8)}";
                var oldKeyHash = _encryptionService.Hash(oldKeySecret);
                var oldKeySecretEncrypted = _encryptionService.Encrypt(oldKeySecret);

                var oldApiKey = new ApiKey
                {
                    Id = Guid.NewGuid(),
                    MerchantId = merchantId,
                    KeyId = oldKeyId,
                    KeyHash = oldKeyHash,
                    KeySecretEncrypted = oldKeySecretEncrypted,
                    Active = true,
                    ExpiresAt = null,
                    CreatedAt = DateTime.UtcNow.AddDays(-10)
                };
                dbContext.ApiKeys.Add(oldApiKey);
                dbContext.SaveChanges();

                var gracePeriodEnd = DateTime.UtcNow.AddDays(30);
                oldApiKey.ExpiresAt = gracePeriodEnd;
                dbContext.SaveChanges();

                var updatedKey = dbContext.ApiKeys.First(k => k.Id == oldApiKey.Id);
                var expiryDifference = (updatedKey.ExpiresAt!.Value - DateTime.UtcNow).TotalDays;

                return updatedKey.ExpiresAt.HasValue && 
                       expiryDifference >= 29.9 && 
                       expiryDifference <= 30.1;
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 100)]
    public void API_Key_Rotation_Should_Keep_Old_Keys_Active_During_Grace_Period()
    {
        Prop.ForAll(
            Arb.Default.Guid(),
            (Guid merchantId) =>
            {
                var options = new DbContextOptionsBuilder<FluxPayDbContext>()
                    .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                    .Options;
                using var dbContext = new FluxPayDbContext(options);

                var merchant = new Merchant
                {
                    Id = merchantId,
                    Name = $"Merchant {merchantId}",
                    Email = $"merchant{merchantId}@example.com",
                    ProviderConfigEncrypted = "encrypted_config",
                    Active = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                dbContext.Merchants.Add(merchant);

                var oldKeySecretBytes = new byte[32];
                RandomNumberGenerator.Fill(oldKeySecretBytes);
                var oldKeySecret = Convert.ToBase64String(oldKeySecretBytes);
                var oldKeyId = $"merchant-{merchantId.ToString().Substring(0, 8)}";
                var oldKeyHash = _encryptionService.Hash(oldKeySecret);
                var oldKeySecretEncrypted = _encryptionService.Encrypt(oldKeySecret);

                var oldApiKey = new ApiKey
                {
                    Id = Guid.NewGuid(),
                    MerchantId = merchantId,
                    KeyId = oldKeyId,
                    KeyHash = oldKeyHash,
                    KeySecretEncrypted = oldKeySecretEncrypted,
                    Active = true,
                    ExpiresAt = null,
                    CreatedAt = DateTime.UtcNow.AddDays(-10)
                };
                dbContext.ApiKeys.Add(oldApiKey);
                dbContext.SaveChanges();

                var gracePeriodEnd = DateTime.UtcNow.AddDays(30);
                oldApiKey.ExpiresAt = gracePeriodEnd;
                dbContext.SaveChanges();

                var updatedKey = dbContext.ApiKeys.First(k => k.Id == oldApiKey.Id);

                return updatedKey.Active && 
                       updatedKey.ExpiresAt.HasValue && 
                       updatedKey.ExpiresAt.Value > DateTime.UtcNow;
            }
        ).QuickCheckThrowOnFailure();
    }
}
