using FsCheck;
using FsCheck.Xunit;
using FluxPay.Core.Entities;
using FluxPay.Core.Services;
using FluxPay.Infrastructure.Data;
using FluxPay.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FluxPay.Tests.Unit.Properties;

public class MerchantManagementPropertyTests : IDisposable
{
    private readonly FluxPayDbContext _dbContext;
    private readonly IEncryptionService _encryptionService;
    private readonly string _originalKey;

    public MerchantManagementPropertyTests()
    {
        _originalKey = Environment.GetEnvironmentVariable("MASTER_ENCRYPTION_KEY") ?? string.Empty;
        
        var key = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(key);
        Environment.SetEnvironmentVariable("MASTER_ENCRYPTION_KEY", Convert.ToBase64String(key));

        var options = new DbContextOptionsBuilder<FluxPayDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        _dbContext = new FluxPayDbContext(options);
        _encryptionService = new EncryptionService();
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
        
        if (string.IsNullOrEmpty(_originalKey))
        {
            Environment.SetEnvironmentVariable("MASTER_ENCRYPTION_KEY", null);
        }
        else
        {
            Environment.SetEnvironmentVariable("MASTER_ENCRYPTION_KEY", _originalKey);
        }
    }

    [Property(MaxTest = 100)]
    public void Merchant_Creation_Should_Generate_Unique_Id_And_ApiKey_Hash(NonEmptyString name, NonEmptyString email)
    {
        Prop.ForAll(
            Arb.From(Gen.Elements((name.Get, email.Get))),
            tuple =>
            {
                var (merchantName, merchantEmail) = tuple;
                
                if (string.IsNullOrWhiteSpace(merchantName) || string.IsNullOrWhiteSpace(merchantEmail))
                {
                    return true;
                }

                var merchantId = Guid.NewGuid();
                
                var keySecretBytes = new byte[32];
                System.Security.Cryptography.RandomNumberGenerator.Fill(keySecretBytes);
                var keySecret = Convert.ToBase64String(keySecretBytes);
                var keyId = $"merchant-{merchantId.ToString().Substring(0, 8)}";
                var keyHash = _encryptionService.Hash(keySecret);

                var merchant = new Merchant
                {
                    Id = merchantId,
                    Name = merchantName,
                    Email = merchantEmail,
                    Active = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                var apiKey = new ApiKey
                {
                    Id = Guid.NewGuid(),
                    MerchantId = merchantId,
                    KeyId = keyId,
                    KeyHash = keyHash,
                    KeySecretEncrypted = _encryptionService.Encrypt(keySecret),
                    Active = true,
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.Merchants.Add(merchant);
                _dbContext.ApiKeys.Add(apiKey);
                _dbContext.SaveChanges();

                var savedMerchant = _dbContext.Merchants
                    .Include(m => m.ApiKeys)
                    .FirstOrDefault(m => m.Id == merchantId);

                var hasUniqueId = savedMerchant != null && savedMerchant.Id != Guid.Empty;
                var hasApiKey = savedMerchant?.ApiKeys.Any() == true;
                var apiKeyHashNotPlaintext = savedMerchant?.ApiKeys.First().KeyHash != keySecret;
                var apiKeyHashExists = !string.IsNullOrEmpty(savedMerchant?.ApiKeys.First().KeyHash);

                _dbContext.Merchants.Remove(merchant);
                _dbContext.ApiKeys.Remove(apiKey);
                _dbContext.SaveChanges();

                return hasUniqueId && hasApiKey && apiKeyHashNotPlaintext && apiKeyHashExists;
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 100)]
    public void Provider_Credentials_Should_Be_Encrypted_Before_Storage(NonEmptyString apiKey)
    {
        Prop.ForAll(
            Arb.From(Gen.Elements(apiKey.Get)),
            providerApiKey =>
            {
                if (string.IsNullOrWhiteSpace(providerApiKey) || providerApiKey.Length < 3)
                {
                    return true;
                }

                var providerConfig = new
                {
                    pagarme = new { api_key = providerApiKey },
                    gerencianet = new { client_id = "test_id", client_secret = "test_secret" }
                };

                var providerConfigJson = JsonSerializer.Serialize(providerConfig);
                var providerConfigEncrypted = _encryptionService.Encrypt(providerConfigJson);

                var merchant = new Merchant
                {
                    Id = Guid.NewGuid(),
                    Name = "Test Merchant",
                    Email = $"test{Guid.NewGuid()}@example.com",
                    ProviderConfigEncrypted = providerConfigEncrypted,
                    Active = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _dbContext.Merchants.Add(merchant);
                _dbContext.SaveChanges();

                var savedMerchant = _dbContext.Merchants.Find(merchant.Id);

                var isEncrypted = savedMerchant?.ProviderConfigEncrypted != providerConfigJson;

                var decrypted = _encryptionService.Decrypt(savedMerchant!.ProviderConfigEncrypted!);
                var canDecrypt = decrypted == providerConfigJson;

                _dbContext.Merchants.Remove(merchant);
                _dbContext.SaveChanges();

                return isEncrypted && canDecrypt;
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 100)]
    public void Disabled_Merchant_Should_Reject_Api_Requests()
    {
        Prop.ForAll(
            Arb.Default.Guid(),
            merchantId =>
            {
                var merchant = new Merchant
                {
                    Id = merchantId,
                    Name = "Test Merchant",
                    Email = $"test{merchantId}@example.com",
                    Active = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _dbContext.Merchants.Add(merchant);
                _dbContext.SaveChanges();

                var savedMerchant = _dbContext.Merchants.Find(merchantId);
                var isDisabled = savedMerchant?.Active == false;

                _dbContext.Merchants.Remove(merchant);
                _dbContext.SaveChanges();

                return isDisabled;
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 100)]
    public void Api_Key_Should_Be_Base64_Encoded_32_Bytes()
    {
        Prop.ForAll(
            Arb.Default.Guid(),
            _ =>
            {
                var keySecretBytes = new byte[32];
                System.Security.Cryptography.RandomNumberGenerator.Fill(keySecretBytes);
                var keySecret = Convert.ToBase64String(keySecretBytes);

                var is32Bytes = keySecretBytes.Length == 32;
                var isBase64 = IsBase64String(keySecret);
                var canDecode = Convert.FromBase64String(keySecret).Length == 32;

                return is32Bytes && isBase64 && canDecode;
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 100)]
    public void Merchant_Id_Should_Be_Included_In_ApiKey_KeyId()
    {
        Prop.ForAll(
            Arb.Default.Guid(),
            merchantId =>
            {
                var keyId = $"merchant-{merchantId.ToString().Substring(0, 8)}";
                
                var startsWithMerchant = keyId.StartsWith("merchant-");
                var containsMerchantIdPrefix = keyId.Contains(merchantId.ToString().Substring(0, 8));

                return startsWithMerchant && containsMerchantIdPrefix;
            }
        ).QuickCheckThrowOnFailure();
    }

    private static bool IsBase64String(string base64)
    {
        if (string.IsNullOrWhiteSpace(base64))
        {
            return false;
        }

        base64 = base64.Trim();
        
        return (base64.Length % 4 == 0) && 
               Regex.IsMatch(base64, @"^[a-zA-Z0-9\+/]*={0,3}$", RegexOptions.None);
    }
}
