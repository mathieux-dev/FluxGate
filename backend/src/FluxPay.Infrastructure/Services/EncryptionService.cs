using System.Security.Cryptography;
using System.Text;
using FluxPay.Core.Services;

namespace FluxPay.Infrastructure.Services;

public class EncryptionService : IEncryptionService
{
    private readonly byte[] _encryptionKey;

    public EncryptionService()
    {
        var keyBase64 = Environment.GetEnvironmentVariable("MASTER_ENCRYPTION_KEY");
        if (string.IsNullOrEmpty(keyBase64))
        {
            throw new InvalidOperationException("MASTER_ENCRYPTION_KEY environment variable is not set");
        }

        _encryptionKey = Convert.FromBase64String(keyBase64);
        
        if (_encryptionKey.Length != 32)
        {
            throw new InvalidOperationException("MASTER_ENCRYPTION_KEY must be 32 bytes (256 bits) when base64 decoded");
        }
    }

    public string Encrypt(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
        {
            throw new ArgumentException("Plaintext cannot be null or empty", nameof(plaintext));
        }

        using var aes = new AesGcm(_encryptionKey, AesGcm.TagByteSizes.MaxSize);
        
        var nonce = new byte[AesGcm.NonceByteSizes.MaxSize];
        RandomNumberGenerator.Fill(nonce);
        
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];
        
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);
        
        var result = new byte[nonce.Length + tag.Length + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, result, nonce.Length, tag.Length);
        Buffer.BlockCopy(ciphertext, 0, result, nonce.Length + tag.Length, ciphertext.Length);
        
        return Convert.ToBase64String(result);
    }

    public string Decrypt(string ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext))
        {
            throw new ArgumentException("Ciphertext cannot be null or empty", nameof(ciphertext));
        }

        var encryptedData = Convert.FromBase64String(ciphertext);
        
        var nonceSize = AesGcm.NonceByteSizes.MaxSize;
        var tagSize = AesGcm.TagByteSizes.MaxSize;
        
        if (encryptedData.Length < nonceSize + tagSize)
        {
            throw new ArgumentException("Invalid ciphertext format", nameof(ciphertext));
        }
        
        var nonce = new byte[nonceSize];
        var tag = new byte[tagSize];
        var ciphertextBytes = new byte[encryptedData.Length - nonceSize - tagSize];
        
        Buffer.BlockCopy(encryptedData, 0, nonce, 0, nonceSize);
        Buffer.BlockCopy(encryptedData, nonceSize, tag, 0, tagSize);
        Buffer.BlockCopy(encryptedData, nonceSize + tagSize, ciphertextBytes, 0, ciphertextBytes.Length);
        
        using var aes = new AesGcm(_encryptionKey, tagSize);
        
        var plaintextBytes = new byte[ciphertextBytes.Length];
        aes.Decrypt(nonce, ciphertextBytes, tag, plaintextBytes);
        
        return Encoding.UTF8.GetString(plaintextBytes);
    }

    public string Hash(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            throw new ArgumentException("Input cannot be null or empty", nameof(input));
        }

        var inputBytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = SHA256.HashData(inputBytes);
        return Convert.ToBase64String(hashBytes);
    }

    public bool VerifyHash(string input, string hash)
    {
        if (string.IsNullOrEmpty(input))
        {
            throw new ArgumentException("Input cannot be null or empty", nameof(input));
        }

        if (string.IsNullOrEmpty(hash))
        {
            throw new ArgumentException("Hash cannot be null or empty", nameof(hash));
        }

        var computedHash = Hash(input);
        return CryptographicOperations.FixedTimeEquals(
            Convert.FromBase64String(computedHash),
            Convert.FromBase64String(hash)
        );
    }
}
