using FsCheck;
using FsCheck.Xunit;
using FluxPay.Infrastructure.Services;

namespace FluxPay.Tests.Unit.Properties;

public class EncryptionPropertyTests : IDisposable
{
    private readonly string _originalKey;

    public EncryptionPropertyTests()
    {
        _originalKey = Environment.GetEnvironmentVariable("MASTER_ENCRYPTION_KEY") ?? string.Empty;
        
        var key = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(key);
        Environment.SetEnvironmentVariable("MASTER_ENCRYPTION_KEY", Convert.ToBase64String(key));
    }

    public void Dispose()
    {
        if (string.IsNullOrEmpty(_originalKey))
        {
            Environment.SetEnvironmentVariable("MASTER_ENCRYPTION_KEY", null);
        }
        else
        {
            Environment.SetEnvironmentVariable("MASTER_ENCRYPTION_KEY", _originalKey);
        }
    }

    [Property(MaxTest = 10)]
    public void Provider_Config_Encryption_RoundTrip_Should_Return_Original_Data(NonEmptyString plaintext)
    {
        Prop.ForAll(
            Arb.From(Gen.Elements(plaintext.Get)),
            text =>
            {
                var service = new EncryptionService();
                var encrypted = service.Encrypt(text);
                var decrypted = service.Decrypt(encrypted);
                return decrypted == text;
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 10)]
    public void Webhook_Secret_Encryption_RoundTrip_Should_Return_Original_Secret(NonEmptyString secret)
    {
        Prop.ForAll(
            Arb.From(Gen.Elements(secret.Get)),
            webhookSecret =>
            {
                var service = new EncryptionService();
                var encrypted = service.Encrypt(webhookSecret);
                var decrypted = service.Decrypt(encrypted);
                return decrypted == webhookSecret;
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 10)]
    public void Hash_Should_Be_Deterministic(NonEmptyString input)
    {
        Prop.ForAll(
            Arb.From(Gen.Elements(input.Get)),
            text =>
            {
                var service = new EncryptionService();
                var hash1 = service.Hash(text);
                var hash2 = service.Hash(text);
                return hash1 == hash2;
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 10)]
    public void VerifyHash_Should_Return_True_For_Matching_Input(NonEmptyString input)
    {
        Prop.ForAll(
            Arb.From(Gen.Elements(input.Get)),
            text =>
            {
                var service = new EncryptionService();
                var hash = service.Hash(text);
                return service.VerifyHash(text, hash);
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 10)]
    public void Encryption_Should_Produce_Different_Ciphertext_For_Same_Plaintext(NonEmptyString plaintext)
    {
        Prop.ForAll(
            Arb.From(Gen.Elements(plaintext.Get)),
            text =>
            {
                var service = new EncryptionService();
                var encrypted1 = service.Encrypt(text);
                var encrypted2 = service.Encrypt(text);
                return encrypted1 != encrypted2;
            }
        ).QuickCheckThrowOnFailure();
    }
}
