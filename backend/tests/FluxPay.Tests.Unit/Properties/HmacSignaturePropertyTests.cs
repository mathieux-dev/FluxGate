using FsCheck;
using FsCheck.Xunit;
using FluxPay.Infrastructure.Services;

namespace FluxPay.Tests.Unit.Properties;

public class HmacSignaturePropertyTests
{
    [Property(MaxTest = 10)]
    public void Webhook_Signature_RoundTrip_Should_Verify_Successfully(
        NonEmptyString secret,
        PositiveInt timestamp,
        Guid nonce,
        NonEmptyString payload)
    {
        Prop.ForAll(
            Arb.From(Gen.Elements((secret.Get, timestamp.Get, nonce, payload.Get))),
            tuple =>
            {
                var (webhookSecret, ts, n, pl) = tuple;
                var service = new HmacSignatureService();
                
                var message = $"{ts}.{n}.{pl}";
                var signature = service.ComputeSignature(webhookSecret, message);
                
                return service.VerifySignature(webhookSecret, message, signature);
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 10)]
    public void API_Signature_RoundTrip_Should_Verify_Successfully(
        NonEmptyString apiKeySecret,
        PositiveInt timestamp,
        Guid nonce,
        NonEmptyString method,
        NonEmptyString path,
        NonEmptyString bodySha256Hex)
    {
        Prop.ForAll(
            Arb.From(Gen.Elements((apiKeySecret.Get, timestamp.Get, nonce, method.Get, path.Get, bodySha256Hex.Get))),
            tuple =>
            {
                var (secret, ts, n, m, p, body) = tuple;
                var service = new HmacSignatureService();
                
                var message = $"{ts}.{n}.{m}.{p}.{body}";
                var signature = service.ComputeSignature(secret, message);
                
                return service.VerifySignature(secret, message, signature);
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 10)]
    public void ComputeSignature_Should_Be_Deterministic(
        NonEmptyString secret,
        NonEmptyString message)
    {
        Prop.ForAll(
            Arb.From(Gen.Elements((secret.Get, message.Get))),
            tuple =>
            {
                var (s, m) = tuple;
                var service = new HmacSignatureService();
                
                var signature1 = service.ComputeSignature(s, m);
                var signature2 = service.ComputeSignature(s, m);
                
                return signature1 == signature2;
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 10)]
    public void VerifySignature_Should_Fail_With_Wrong_Secret(
        NonEmptyString correctSecret,
        NonEmptyString wrongSecret,
        NonEmptyString message)
    {
        Prop.ForAll(
            Arb.From(Gen.Elements((correctSecret.Get, wrongSecret.Get, message.Get))),
            tuple =>
            {
                var (correct, wrong, m) = tuple;
                if (correct == wrong)
                {
                    return true;
                }
                
                var service = new HmacSignatureService();
                var signature = service.ComputeSignature(correct, m);
                
                return !service.VerifySignature(wrong, m, signature);
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 10)]
    public void VerifySignature_Should_Fail_With_Modified_Message(
        NonEmptyString secret,
        NonEmptyString originalMessage,
        NonEmptyString modifiedMessage)
    {
        Prop.ForAll(
            Arb.From(Gen.Elements((secret.Get, originalMessage.Get, modifiedMessage.Get))),
            tuple =>
            {
                var (s, original, modified) = tuple;
                if (original == modified)
                {
                    return true;
                }
                
                var service = new HmacSignatureService();
                var signature = service.ComputeSignature(s, original);
                
                return !service.VerifySignature(s, modified, signature);
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 10)]
    public void Different_Messages_Should_Produce_Different_Signatures(
        NonEmptyString secret,
        NonEmptyString message1,
        NonEmptyString message2)
    {
        Prop.ForAll(
            Arb.From(Gen.Elements((secret.Get, message1.Get, message2.Get))),
            tuple =>
            {
                var (s, m1, m2) = tuple;
                if (m1 == m2)
                {
                    return true;
                }
                
                var service = new HmacSignatureService();
                var signature1 = service.ComputeSignature(s, m1);
                var signature2 = service.ComputeSignature(s, m2);
                
                return signature1 != signature2;
            }
        ).QuickCheckThrowOnFailure();
    }
}
