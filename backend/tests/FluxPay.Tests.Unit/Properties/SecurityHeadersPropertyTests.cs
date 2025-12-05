using FsCheck;
using FsCheck.Xunit;
using FluxPay.Api.Middleware;
using Microsoft.AspNetCore.Http;

namespace FluxPay.Tests.Unit.Properties;

public class SecurityHeadersPropertyTests
{
    [Property(MaxTest = 10)]
    public void Security_Headers_Should_Be_Present_In_All_Responses()
    {
        Prop.ForAll(
            Arb.From(Gen.Elements("GET", "POST", "PUT", "DELETE", "PATCH")),
            Arb.From(Gen.Elements("/health", "/v1/payments", "/v1/webhooks", "/v1/admin/merchants")),
            (method, path) =>
            {
                var middleware = new SecurityHeadersMiddleware(_ => Task.CompletedTask);

                var context = new DefaultHttpContext();
                context.Request.Method = method;
                context.Request.Path = path;

                middleware.InvokeAsync(context).Wait();

                var headers = context.Response.Headers;

                var hasStrictTransportSecurity = headers.ContainsKey("Strict-Transport-Security") &&
                    headers["Strict-Transport-Security"].ToString().Contains("max-age=31536000");

                var hasXContentTypeOptions = headers.ContainsKey("X-Content-Type-Options") &&
                    headers["X-Content-Type-Options"].ToString() == "nosniff";

                var hasXFrameOptions = headers.ContainsKey("X-Frame-Options") &&
                    headers["X-Frame-Options"].ToString() == "DENY";

                var hasContentSecurityPolicy = headers.ContainsKey("Content-Security-Policy") &&
                    headers["Content-Security-Policy"].ToString().Contains("default-src 'self'");

                return hasStrictTransportSecurity &&
                       hasXContentTypeOptions &&
                       hasXFrameOptions &&
                       hasContentSecurityPolicy;
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 10)]
    public void Server_Headers_Should_Be_Removed_From_All_Responses()
    {
        Prop.ForAll(
            Arb.From(Gen.Elements("GET", "POST", "PUT", "DELETE", "PATCH")),
            Arb.From(Gen.Elements("/health", "/v1/payments", "/v1/webhooks", "/v1/admin/merchants")),
            (method, path) =>
            {
                var middleware = new SecurityHeadersMiddleware(ctx =>
                {
                    ctx.Response.Headers["Server"] = "TestServer/1.0";
                    ctx.Response.Headers["X-Powered-By"] = "ASP.NET";
                    return Task.CompletedTask;
                });

                var context = new DefaultHttpContext();
                context.Request.Method = method;
                context.Request.Path = path;

                middleware.InvokeAsync(context).Wait();

                var headers = context.Response.Headers;

                var serverHeaderRemoved = !headers.ContainsKey("Server");
                var poweredByHeaderRemoved = !headers.ContainsKey("X-Powered-By");

                return serverHeaderRemoved && poweredByHeaderRemoved;
            }
        ).QuickCheckThrowOnFailure();
    }
}
