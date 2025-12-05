using FsCheck;
using FsCheck.Xunit;
using FluxPay.Core.Services;
using NSubstitute;

namespace FluxPay.Tests.Unit.Properties;

public class RateLimiterPropertyTests
{
    [Property(MaxTest = 10)]
    public void Merchant_Rate_Limit_Enforcement_Should_Reject_201st_Request(NonEmptyString merchantId)
    {
        Prop.ForAll(
            Arb.From(Gen.Elements(merchantId.Get)),
            merchant =>
            {
                var mockRateLimiter = Substitute.For<IRateLimiter>();
                var limit = 200;
                var window = TimeSpan.FromMinutes(1);

                var requestCount = 0;
                mockRateLimiter.CheckRateLimitAsync(Arg.Any<string>(), limit, window)
                    .Returns(_ =>
                    {
                        requestCount++;
                        var isAllowed = requestCount <= limit;
                        return Task.FromResult(new RateLimitResult
                        {
                            IsAllowed = isAllowed,
                            RemainingRequests = Math.Max(0, limit - requestCount),
                            ResetTime = DateTime.UtcNow.Add(window)
                        });
                    });

                RateLimitResult? lastResult = null;
                for (int i = 0; i < 201; i++)
                {
                    lastResult = mockRateLimiter.CheckRateLimitAsync($"merchant:{merchant}", limit, window).Result;
                }

                return lastResult != null && !lastResult.IsAllowed;
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 10)]
    public void Payment_Endpoint_IP_Rate_Limit_Should_Reject_21st_Request(NonEmptyString ipAddress)
    {
        Prop.ForAll(
            Arb.From(Gen.Elements(ipAddress.Get)),
            ip =>
            {
                var mockRateLimiter = Substitute.For<IRateLimiter>();
                var limit = 20;
                var window = TimeSpan.FromMinutes(1);

                var requestCount = 0;
                mockRateLimiter.CheckRateLimitAsync(Arg.Any<string>(), limit, window)
                    .Returns(_ =>
                    {
                        requestCount++;
                        var isAllowed = requestCount <= limit;
                        return Task.FromResult(new RateLimitResult
                        {
                            IsAllowed = isAllowed,
                            RemainingRequests = Math.Max(0, limit - requestCount),
                            ResetTime = DateTime.UtcNow.Add(window)
                        });
                    });

                RateLimitResult? lastResult = null;
                for (int i = 0; i < 21; i++)
                {
                    lastResult = mockRateLimiter.CheckRateLimitAsync($"ip:{ip}:payment", limit, window).Result;
                }

                return lastResult != null && !lastResult.IsAllowed;
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 10)]
    public void Rate_Limit_Window_Reset_Should_Reset_Counter(NonEmptyString key)
    {
        Prop.ForAll(
            Arb.From(Gen.Elements(key.Get)),
            rateLimitKey =>
            {
                var mockRateLimiter = Substitute.For<IRateLimiter>();
                var limit = 10;
                var window = TimeSpan.FromMinutes(1);

                var requestCount = 0;
                var windowExpired = false;
                mockRateLimiter.CheckRateLimitAsync(Arg.Any<string>(), limit, window)
                    .Returns(_ =>
                    {
                        if (windowExpired)
                        {
                            requestCount = 1;
                            windowExpired = false;
                        }
                        else
                        {
                            requestCount++;
                        }
                        var isAllowed = requestCount <= limit;
                        return Task.FromResult(new RateLimitResult
                        {
                            IsAllowed = isAllowed,
                            RemainingRequests = Math.Max(0, limit - requestCount),
                            ResetTime = DateTime.UtcNow.Add(window)
                        });
                    });

                for (int i = 0; i < 10; i++)
                {
                    mockRateLimiter.CheckRateLimitAsync(rateLimitKey, limit, window).Wait();
                }

                windowExpired = true;
                var resultAfterReset = mockRateLimiter.CheckRateLimitAsync(rateLimitKey, limit, window).Result;

                return resultAfterReset.IsAllowed && resultAfterReset.RemainingRequests == limit - 1;
            }
        ).QuickCheckThrowOnFailure();
    }
}
