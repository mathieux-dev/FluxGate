using FluxPay.Core.Services;

namespace FluxPay.Api.Middleware;

public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;

    public RateLimitingMiddleware(RequestDelegate next, ILogger<RateLimitingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IRateLimiter rateLimiter)
    {
        var merchantId = context.Items["MerchantId"]?.ToString();
        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var path = context.Request.Path.Value ?? string.Empty;

        if (!string.IsNullOrEmpty(merchantId))
        {
            var merchantKey = $"merchant:{merchantId}";
            var merchantLimit = 200;
            var merchantWindow = TimeSpan.FromMinutes(1);

            var merchantResult = await rateLimiter.CheckRateLimitAsync(merchantKey, merchantLimit, merchantWindow);

            if (!merchantResult.IsAllowed)
            {
                var retryAfter = (int)(merchantResult.ResetTime - DateTime.UtcNow).TotalSeconds;
                context.Response.StatusCode = 429;
                context.Response.Headers["Retry-After"] = retryAfter.ToString();
                await context.Response.WriteAsJsonAsync(new
                {
                    error = new
                    {
                        code = "RATE_LIMIT_EXCEEDED",
                        message = "Too many requests in time window"
                    }
                });
                return;
            }
        }

        if (path.StartsWith("/v1/payments") && context.Request.Method == "POST")
        {
            var ipKey = $"ip:{ipAddress}:payments";
            var ipLimit = 20;
            var ipWindow = TimeSpan.FromMinutes(1);

            var ipResult = await rateLimiter.CheckRateLimitAsync(ipKey, ipLimit, ipWindow);

            if (!ipResult.IsAllowed)
            {
                var retryAfter = (int)(ipResult.ResetTime - DateTime.UtcNow).TotalSeconds;
                context.Response.StatusCode = 429;
                context.Response.Headers["Retry-After"] = retryAfter.ToString();
                await context.Response.WriteAsJsonAsync(new
                {
                    error = new
                    {
                        code = "RATE_LIMIT_EXCEEDED",
                        message = "Too many requests in time window"
                    }
                });
                return;
            }
        }

        await _next(context);
    }
}
