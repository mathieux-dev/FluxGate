using StackExchange.Redis;

namespace FluxPay.Infrastructure.Redis;

public class RedisConnectionFactory
{
    private readonly Lazy<ConnectionMultiplexer> _connection;

    public RedisConnectionFactory(string connectionString)
    {
        _connection = new Lazy<ConnectionMultiplexer>(() => 
            ConnectionMultiplexer.Connect(connectionString));
    }

    public IDatabase GetDatabase()
    {
        return _connection.Value.GetDatabase();
    }

    public IConnectionMultiplexer GetConnection()
    {
        return _connection.Value;
    }
}
