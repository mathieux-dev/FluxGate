using FluxPay.Core.Services;
using FluxPay.Infrastructure.Redis;
using StackExchange.Redis;

namespace FluxPay.Infrastructure.Services;

public class NonceStore : INonceStore
{
    private readonly RedisConnectionFactory _redisFactory;

    public NonceStore(RedisConnectionFactory redisFactory)
    {
        _redisFactory = redisFactory;
    }

    public async Task<bool> IsNonceUniqueAsync(string merchantId, string nonce)
    {
        var db = _redisFactory.GetDatabase();
        var key = $"nonce:{merchantId}:{nonce}";
        var exists = await db.KeyExistsAsync(key);
        return !exists;
    }

    public async Task StoreNonceAsync(string merchantId, string nonce, TimeSpan ttl)
    {
        var db = _redisFactory.GetDatabase();
        var key = $"nonce:{merchantId}:{nonce}";
        await db.StringSetAsync(key, "1", ttl);
    }
}
