using StackExchange.Redis;
using Testcontainers.Redis;

namespace Urban.IntegrationTests.Identity.Sessions;

/// <summary>
/// Um Redis real (mesma versão da produção) por execução de testes. Os testes compartilham o servidor e se isolam
/// usando identificadores únicos.
/// </summary>
public sealed class RedisFixture : IAsyncLifetime
{
    private readonly RedisContainer _container = new RedisBuilder("redis:8").Build();

    public IConnectionMultiplexer Connection { get; private set; } = null!;

    public IDatabase Database => Connection.GetDatabase();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        Connection = await ConnectionMultiplexer.ConnectAsync(_container.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        await Connection.DisposeAsync();
        await _container.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class RedisCollection : ICollectionFixture<RedisFixture>
{
    public const string Name = "redis";
}