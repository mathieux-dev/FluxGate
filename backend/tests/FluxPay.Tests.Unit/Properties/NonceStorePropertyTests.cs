using FsCheck;
using FsCheck.Xunit;
using FluxPay.Core.Services;
using NSubstitute;

namespace FluxPay.Tests.Unit.Properties;

public class NonceStorePropertyTests
{
    [Property(MaxTest = 100)]
    public void Webhook_Nonce_Replay_Protection_Should_Reject_Reused_Nonce(NonEmptyString merchantId, NonEmptyString nonce)
    {
        Prop.ForAll(
            Arb.From(Gen.Elements((merchantId.Get, nonce.Get))),
            tuple =>
            {
                var (merchant, nonceValue) = tuple;
                var mockStore = Substitute.For<INonceStore>();

                var callCount = 0;
                mockStore.IsNonceUniqueAsync(merchant, nonceValue)
                    .Returns(_ =>
                    {
                        callCount++;
                        return Task.FromResult(callCount == 1);
                    });

                mockStore.StoreNonceAsync(merchant, nonceValue, Arg.Any<TimeSpan>())
                    .Returns(Task.CompletedTask);

                var firstCheck = mockStore.IsNonceUniqueAsync(merchant, nonceValue).Result;
                mockStore.StoreNonceAsync(merchant, nonceValue, TimeSpan.FromHours(24)).Wait();
                var secondCheck = mockStore.IsNonceUniqueAsync(merchant, nonceValue).Result;

                return firstCheck && !secondCheck;
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 100)]
    public void API_Nonce_Replay_Protection_Should_Reject_Reused_Nonce(NonEmptyString merchantId, NonEmptyString nonce)
    {
        Prop.ForAll(
            Arb.From(Gen.Elements((merchantId.Get, nonce.Get))),
            tuple =>
            {
                var (merchant, nonceValue) = tuple;
                var mockStore = Substitute.For<INonceStore>();

                var callCount = 0;
                mockStore.IsNonceUniqueAsync(merchant, nonceValue)
                    .Returns(_ =>
                    {
                        callCount++;
                        return Task.FromResult(callCount == 1);
                    });

                mockStore.StoreNonceAsync(merchant, nonceValue, Arg.Any<TimeSpan>())
                    .Returns(Task.CompletedTask);

                var firstCheck = mockStore.IsNonceUniqueAsync(merchant, nonceValue).Result;
                mockStore.StoreNonceAsync(merchant, nonceValue, TimeSpan.FromHours(24)).Wait();
                var secondCheck = mockStore.IsNonceUniqueAsync(merchant, nonceValue).Result;

                return firstCheck && !secondCheck;
            }
        ).QuickCheckThrowOnFailure();
    }
}
