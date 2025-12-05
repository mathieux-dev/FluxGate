using FsCheck;
using FsCheck.Xunit;
using FluxPay.Api.Middleware;
using FluxPay.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FluxPay.Tests.Unit.Properties;

public class RateLimitMiddlewarePropertyTests
{
    [Property(MaxTest = 10)]
    public void Rate_Limit_Response_Headers_Should_Include_Retry_After(PositiveInt requestCount)
    {
        Prop.ForAll(
            Arb.From(Gen.Choose(201, 300)),
            count =>
            {
                var mockRateLimiter = Substitute.For<IRateLimiter>();
                var mockLogger = Substitute.For<ILogger<RateLimitingMiddleware>>();
                var middleware = new RateLimitingMiddleware(
                    _ => Task.CompletedTask,
                    mockLogger
                );

                var context = new DefaultHttpContext();
                context.Items["MerchantId"] = Guid.NewGuid().ToString();
                context.Request.Method = "POST";
                context.Request.Path = "/v1/payments";
                context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.1");

                var resetTime = DateTime.UtcNow.AddMinutes(1);
                mockRateLimiter.CheckRateLimitAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<TimeSpan>())
                    .Returns(Task.FromResult(new RateLimitResult
                    {
                        IsAllowed = false,
                        RemainingRequests = 0,
                        ResetTime = resetTime
                    }));

                middleware.InvokeAsync(context, mockRateLimiter).Wait();

                var hasRetryAfterHeader = context.Response.Headers.ContainsKey("Retry-After");
                var statusCodeIs429 = context.Response.StatusCode == 429;

                if (hasRetryAfterHeader)
                {
                    var retryAfterValue = context.Response.Headers["Retry-After"].ToString();
                    var canParseRetryAfter = int.TryParse(retryAfterValue, out var retryAfterSeconds);
                    return statusCodeIs429 && canParseRetryAfter && retryAfterSeconds > 0;
                }

                return false;
            }
        ).QuickCheckThrowOnFailure();
    }
}
