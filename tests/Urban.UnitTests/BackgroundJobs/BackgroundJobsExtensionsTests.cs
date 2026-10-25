using FluentAssertions;
using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Urban.BackgroundJobs;

namespace Urban.UnitTests.BackgroundJobs;

public class BackgroundJobsExtensionsTests
{
    private const string Postgres = "Host=localhost;Database=unused;Username=unused;Password=unused";

    [Fact]
    public void AddBackgroundJobs_WithoutThePostgresConnectionString_Refuses()
    {
        Action register = () => Register([]);

        register.Should().Throw<InvalidOperationException>().WithMessage("*ConnectionStrings:Postgres*");
    }

    [Fact]
    public void AddBackgroundJobs_RegistersTheClientThatModulesUseToQueueJobs()
    {
        ServiceCollection services = Register(WithPostgres());

        services.Should().Contain(d => d.ServiceType == typeof(IBackgroundJobClient));
    }

    [Fact]
    public void AddBackgroundJobs_ByDefault_ProcessesTheQueueInThisProcess()
    {
        ServiceCollection services = Register(WithPostgres());

        services.Where(d => d.ServiceType == typeof(IHostedService)).Should().ContainSingle();
    }

    [Fact]
    public void AddBackgroundJobs_WithTheServerTurnedOff_OnlyQueues()
    {
        ServiceCollection services = Register(WithPostgres(("BackgroundJobs:RunServer", "false")));

        services.Should().NotContain(d => d.ServiceType == typeof(IHostedService));
        services.Should().Contain(d => d.ServiceType == typeof(IBackgroundJobClient));
    }

    [Fact]
    public void AddBackgroundJobs_WithAnInvalidWorkerCount_Refuses()
    {
        Action register = () => Register(WithPostgres(("BackgroundJobs:WorkerCount", "0")));

        register.Should().Throw<InvalidOperationException>().WithMessage("*BackgroundJobs:WorkerCount*");
    }

    private static Dictionary<string, string?> WithPostgres(params (string Key, string Value)[] more)
    {
        Dictionary<string, string?> settings = new() { ["ConnectionStrings:Postgres"] = Postgres };
        foreach ((string key, string value) in more)
        {
            settings[key] = value;
        }

        return settings;
    }

    private static ServiceCollection Register(Dictionary<string, string?> settings)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        ServiceCollection services = new();
        services.AddBackgroundJobs(configuration);

        return services;
    }
}