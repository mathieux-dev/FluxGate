namespace FluxPay.Core.Services;

public interface INonceStore
{
    Task<bool> IsNonceUniqueAsync(string merchantId, string nonce);
    Task StoreNonceAsync(string merchantId, string nonce, TimeSpan ttl);
}
