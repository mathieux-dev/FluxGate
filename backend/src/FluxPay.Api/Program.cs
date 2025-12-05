using FluxPay.Api.Filters;
using FluxPay.Api.Middleware;
using FluxPay.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

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

app.UseAuthorization();
app.MapControllers();

app.Run();
