namespace FluxPay.Core.Configuration;

public class RedisSettings
{
    public string ConnectionString { get; set; } = string.Empty;
    public int DefaultTtlMinutes { get; set; } = 1440;
    public bool AbortOnConnectFail { get; set; } = false;
}
