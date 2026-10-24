using Urban.IntegrationTests.Identity.Persistence;
using Urban.IntegrationTests.Identity.Sessions;

namespace Urban.IntegrationTests.Api;

public sealed class ApiFixture : IAsyncLifetime
{
    private readonly RedisFixture _redis = new();

    public PostgresFixture Postgres { get; } = new();

    public ApiFactory Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(Postgres.InitializeAsync(), _redis.InitializeAsync());

        Factory = new ApiFactory
        {
            PostgresConnectionString = Postgres.ConnectionString,
            RedisConnectionString = _redis.ConnectionString,
        };
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _redis.DisposeAsync();
        await Postgres.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "api";
}