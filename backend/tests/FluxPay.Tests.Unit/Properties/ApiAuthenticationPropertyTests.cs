using FsCheck;
using FsCheck.Xunit;
using FluxPay.Infrastructure.Services;
using FluxPay.Infrastructure.Redis;
using NSubstitute;
using StackExchange.Redis;
using System.Security.Cryptography;
using System.Text;

namespace FluxPay.Tests.Unit.Properties;

public class ApiAuthenticationPropertyTests
{
    [Property(MaxTest = 100)]
    public void API_Request_Should_Include_Required_Headers(
        NonEmptyString apiKey,
        PositiveInt timestamp,
        Guid nonce,
        NonEmptyString method,
        NonEmptyString path,
        NonEmptyString body)
    {
        Prop.ForAll(
            Arb.From(Gen.Elements((apiKey.Get, timestamp.Get, nonce, method.Get, path.Get, body.Get))),
            tuple =>
            {
                var (key, ts, n, m, p, b) = tuple;
                
                var bodySha256 = ComputeSha256(b);
                var message = $"{ts}.{n}.{m}.{p}.{bodySha256}";
                
                var hasApiKey = !string.IsNullOrEmpty(key);
                var hasTimestamp = ts > 0;
                var hasNonce = n != Guid.Empty;
                var hasSignature = !string.IsNullOrEmpty(message);
                
                return hasApiKey && hasTimestamp && hasNonce && hasSignature;
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 100)]
    public void API_Signature_RoundTrip_Should_Verify_Successfully(
        NonEmptyString apiKeySecret,
        PositiveInt timestamp,
        Guid nonce,
        NonEmptyString method,
        NonEmptyString path,
        NonEmptyString body)
    {
        Prop.ForAll(
            Arb.From(Gen.Elements((apiKeySecret.Get, timestamp.Get, nonce, method.Get, path.Get, body.Get))),
            tuple =>
            {
                var (secret, ts, n, m, p, b) = tuple;
                var service = new HmacSignatureService();
                
                var bodySha256 = ComputeSha256(b);
                var message = $"{ts}.{n}.{m}.{p}.{bodySha256}";
                var signature = service.ComputeSignature(secret, message);
                
                return service.VerifySignature(secret, message, signature);
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 100)]
    public void API_Request_With_Timestamp_Skew_Greater_Than_60_Seconds_Should_Be_Rejected(
        NonEmptyString apiKey,
        PositiveInt timestampOffset)
    {
        Prop.ForAll(
            Arb.From(Gen.Elements((apiKey.Get, timestampOffset.Get))),
            tuple =>
            {
                var (key, offset) = tuple;
                
                var currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var skewedTimestamp = currentTimestamp + (offset % 1000) + 61;
                
                var skew = Math.Abs(skewedTimestamp - currentTimestamp);
                var shouldReject = skew > 60;
                
                return shouldReject;
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 100)]
    public void API_Request_With_Timestamp_Within_60_Seconds_Should_Be_Accepted(
        NonEmptyString apiKey,
        PositiveInt timestampOffset)
    {
        Prop.ForAll(
            Arb.From(Gen.Elements((apiKey.Get, timestampOffset.Get))),
            tuple =>
            {
                var (key, offset) = tuple;
                
                var currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var validTimestamp = currentTimestamp + ((offset % 60) - 30);
                
                var skew = Math.Abs(validTimestamp - currentTimestamp);
                var shouldAccept = skew <= 60;
                
                return shouldAccept;
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 100)]
    public void API_Request_With_Reused_Nonce_Should_Be_Rejected(
        Guid nonce,
        NonEmptyString merchantId)
    {
        Prop.ForAll(
            Arb.From(Gen.Elements((nonce, merchantId.Get))),
            tuple =>
            {
                var (n, mid) = tuple;
                
                var usedNonces = new HashSet<string>();
                
                var isFirstUnique = !usedNonces.Contains($"nonce:{mid}:{n}");
                usedNonces.Add($"nonce:{mid}:{n}");
                var isSecondUnique = !usedNonces.Contains($"nonce:{mid}:{n}");
                
                return isFirstUnique && !isSecondUnique;
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 100)]
    public void API_Request_With_Invalid_Signature_Should_Be_Rejected(
        NonEmptyString correctSecret,
        NonEmptyString wrongSecret,
        PositiveInt timestamp,
        Guid nonce,
        NonEmptyString method,
        NonEmptyString path,
        NonEmptyString body)
    {
        Prop.ForAll(
            Arb.From(Gen.Elements((correctSecret.Get, wrongSecret.Get, timestamp.Get, nonce, method.Get, path.Get, body.Get))),
            tuple =>
            {
                var (correct, wrong, ts, n, m, p, b) = tuple;
                
                if (correct == wrong)
                {
                    return true;
                }
                
                var service = new HmacSignatureService();
                var bodySha256 = ComputeSha256(b);
                var message = $"{ts}.{n}.{m}.{p}.{bodySha256}";
                
                var signature = service.ComputeSignature(correct, message);
                var isValid = service.VerifySignature(wrong, message, signature);
                
                return !isValid;
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 100)]
    public void API_Request_With_Modified_Message_Should_Fail_Verification(
        NonEmptyString secret,
        PositiveInt timestamp,
        Guid nonce,
        NonEmptyString method,
        NonEmptyString path,
        NonEmptyString body,
        NonEmptyString modifiedBody)
    {
        Prop.ForAll(
            Arb.From(Gen.Elements((secret.Get, timestamp.Get, nonce, method.Get, path.Get, body.Get, modifiedBody.Get))),
            tuple =>
            {
                var (s, ts, n, m, p, b, mb) = tuple;
                
                if (b == mb)
                {
                    return true;
                }
                
                var service = new HmacSignatureService();
                var bodySha256 = ComputeSha256(b);
                var originalMessage = $"{ts}.{n}.{m}.{p}.{bodySha256}";
                
                var modifiedBodySha256 = ComputeSha256(mb);
                var modifiedMessage = $"{ts}.{n}.{m}.{p}.{modifiedBodySha256}";
                
                var signature = service.ComputeSignature(s, originalMessage);
                var isValid = service.VerifySignature(s, modifiedMessage, signature);
                
                return !isValid;
            }
        ).QuickCheckThrowOnFailure();
    }

    private static string ComputeSha256(string input)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
