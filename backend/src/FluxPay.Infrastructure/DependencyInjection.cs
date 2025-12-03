using FluxPay.Core.Configuration;
using FluxPay.Core.Services;
using FluxPay.Infrastructure.Data;
using FluxPay.Infrastructure.Redis;
using FluxPay.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FluxPay.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var databaseSettings = configuration.GetSection("Database").Get<DatabaseSettings>() 
            ?? new DatabaseSettings();
        var redisSettings = configuration.GetSection("Redis").Get<RedisSettings>() 
            ?? new RedisSettings();

        services.AddDbContext<FluxPayDbContext>(options =>
        {
            options.UseNpgsql(
                databaseSettings.ConnectionString,
                npgsqlOptions =>
                {
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: databaseSettings.MaxRetryCount,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorCodesToAdd: null);
                    npgsqlOptions.CommandTimeout(databaseSettings.CommandTimeout);
                });

            if (databaseSettings.EnableSensitiveDataLogging)
            {
                options.EnableSensitiveDataLogging();
            }
        });

        services.AddSingleton(sp => 
            new RedisConnectionFactory(redisSettings.ConnectionString));

        services.AddSingleton<IEncryptionService, EncryptionService>();
        services.AddSingleton<INonceStore, NonceStore>();

        return services;
    }
}
