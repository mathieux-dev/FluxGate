using System.Security.Cryptography;
using System.Text;
using FluxPay.Core.Services;

namespace FluxPay.Infrastructure.Services;

public class HmacSignatureService : IHmacSignatureService
{
    public string ComputeSignature(string secret, string message)
    {
        if (string.IsNullOrEmpty(secret))
        {
            throw new ArgumentException("Secret cannot be null or empty", nameof(secret));
        }

        if (string.IsNullOrEmpty(message))
        {
            throw new ArgumentException("Message cannot be null or empty", nameof(message));
        }

        var secretBytes = Encoding.UTF8.GetBytes(secret);
        var messageBytes = Encoding.UTF8.GetBytes(message);
        
        using var hmac = new HMACSHA256(secretBytes);
        var hashBytes = hmac.ComputeHash(messageBytes);
        
        return Convert.ToBase64String(hashBytes);
    }

    public bool VerifySignature(string secret, string message, string signature)
    {
        if (string.IsNullOrEmpty(secret))
        {
            throw new ArgumentException("Secret cannot be null or empty", nameof(secret));
        }

        if (string.IsNullOrEmpty(message))
        {
            throw new ArgumentException("Message cannot be null or empty", nameof(message));
        }

        if (string.IsNullOrEmpty(signature))
        {
            throw new ArgumentException("Signature cannot be null or empty", nameof(signature));
        }

        try
        {
            var computedSignature = ComputeSignature(secret, message);
            
            var computedBytes = Convert.FromBase64String(computedSignature);
            var providedBytes = Convert.FromBase64String(signature);
            
            return CryptographicOperations.FixedTimeEquals(computedBytes, providedBytes);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
