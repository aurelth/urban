using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Infrastructure;
using Urban.Identity.Infrastructure.Messaging;

namespace Urban.UnitTests.Identity.Infrastructure;

public class IdentityInfrastructureRegistrationTests
{
    [Fact]
    public void TheEmailNotifierIsScoped_BecauseItHoldsAnHttpClientThatMustFollowDnsChanges()
    {
        Register().Single(d => d.ServiceType == typeof(IEmailVerificationNotifier)).Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void TheSendJobIsScoped_BecauseItReachesTheDatabaseThroughScopedHandlers()
    {
        Register().Single(d => d.ServiceType == typeof(SendEmailVerificationJob)).Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void TheDispatcherQueuesJobsThroughHangfire()
    {
        ServiceDescriptor descriptor = Register().Single(d => d.ServiceType == typeof(IEmailVerificationDispatcher));

        descriptor.ImplementationType.Should().Be<HangfireEmailVerificationDispatcher>();
        descriptor.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void ThePasswordResetNotifierIsScoped_ForTheSameReasonAsTheConfirmationOne()
    {
        Register().Single(d => d.ServiceType == typeof(IPasswordResetNotifier)).Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void ThePasswordResetJobIsScoped_BecauseItReachesTheDatabaseThroughScopedHandlers()
    {
        Register().Single(d => d.ServiceType == typeof(SendPasswordResetJob)).Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void ThePasswordResetDispatcherQueuesJobsThroughHangfire()
    {
        ServiceDescriptor descriptor = Register().Single(d => d.ServiceType == typeof(IPasswordResetDispatcher));

        descriptor.ImplementationType.Should().Be<HangfirePasswordResetDispatcher>();
        descriptor.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void ThePasswordResetStoreIsScoped_BecauseItSharesTheRequestDbContext()
    {
        Register().Single(d => d.ServiceType == typeof(IPasswordResets)).Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    private static ServiceCollection Register()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = "Host=localhost;Database=unused;Username=unused;Password=unused",
                ["ConnectionStrings:Redis"] = "localhost:6379",
                ["Jwt:SigningKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
                ["Links:WebBaseUrl"] = "https://urban-pro.io",
            })
            .Build();

        ServiceCollection services = new();
        services.AddIdentityInfrastructure(configuration);

        return services;
    }
}