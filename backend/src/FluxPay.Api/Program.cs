using FluxPay.Api.Filters;
using FluxPay.Api.Logging;
using FluxPay.Api.Middleware;
using FluxPay.Core.Configuration;
using FluxPay.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

var openTelemetrySettings = builder.Configuration.GetSection("OpenTelemetry").Get<OpenTelemetrySettings>() 
    ?? new OpenTelemetrySettings();

var redisSettings = builder.Configuration.GetSection("Redis").Get<RedisSettings>() 
    ?? new RedisSettings();

var logflareSettings = builder.Configuration.GetSection("Logflare").Get<LogflareSettings>() 
    ?? new LogflareSettings();

var loggerConfig = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.With<SensitiveDataMaskingEnricher>()
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .Enrich.WithProperty("Application", openTelemetrySettings.ServiceName)
    .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
    .WriteTo.Console(new CompactJsonFormatter());

if (!string.IsNullOrEmpty(logflareSettings.ApiKey) && !string.IsNullOrEmpty(logflareSettings.SourceId))
{
    var httpClient = new System.Net.Http.HttpClient();
    httpClient.DefaultRequestHeaders.Add("X-API-KEY", logflareSettings.ApiKey);
    
    loggerConfig.WriteTo.Http(
        requestUri: $"https://api.logflare.app/logs?source={logflareSettings.SourceId}",
        queueLimitBytes: null,
        textFormatter: new CompactJsonFormatter(),
        httpClient: new Serilog.Sinks.Http.HttpClients.JsonHttpClient(httpClient));
}

Log.Logger = loggerConfig.CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(
            serviceName: openTelemetrySettings.ServiceName,
            serviceVersion: openTelemetrySettings.ServiceVersion))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation(options =>
            {
                options.RecordException = true;
                options.Filter = (httpContext) =>
                {
                    return !httpContext.Request.Path.StartsWithSegments("/health");
                };
            })
            .AddHttpClientInstrumentation(options =>
            {
                options.RecordException = true;
            })
            .AddNpgsql()
            .AddRedisInstrumentation(
                StackExchange.Redis.ConnectionMultiplexer.Connect(redisSettings.ConnectionString));

        if (!string.IsNullOrEmpty(openTelemetrySettings.OtlpEndpoint))
        {
            tracing.AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(openTelemetrySettings.OtlpEndpoint);
                
                if (!string.IsNullOrEmpty(openTelemetrySettings.OtlpHeaders))
                {
                    var headers = openTelemetrySettings.OtlpHeaders.Split(',');
                    foreach (var header in headers)
                    {
                        var parts = header.Split('=');
                        if (parts.Length == 2)
                        {
                            options.Headers += $"{parts[0].Trim()}={parts[1].Trim()},";
                        }
                    }
                    options.Headers = options.Headers.TrimEnd(',');
                }
            });
        }
        else
        {
            tracing.AddConsoleExporter();
        }
    });

builder.Services.AddControllers(options =>
    {
        options.Filters.Add<StrictJsonValidationFilter>();
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.AllowTrailingCommas = false;
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.SuppressModelStateInvalidFilter = false;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// Apply migrations automatically on startup
using (var scope = app.Services.CreateScope())
{
    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<FluxPay.Infrastructure.Data.FluxPayDbContext>();
        dbContext.Database.Migrate();
        Log.Information("Database migrations applied successfully");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "An error occurred while applying database migrations");
        throw;
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseSecurityHeaders();

app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/v1/payments") ||
               context.Request.Path.StartsWithSegments("/v1/subscriptions"),
    appBuilder =>
    {
        appBuilder.UseApiKeyAuthentication();
        appBuilder.UseRateLimiting();
    });

app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/v1/merchants"),
    appBuilder =>
    {
        appBuilder.UseJwtAuthentication();
    });

app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/v1/admin"),
    appBuilder =>
    {
        appBuilder.UseIpAllowlist();
        appBuilder.UseJwtAuthentication();
    });

app.UseAuthorization();
app.MapControllers();

app.Run();
