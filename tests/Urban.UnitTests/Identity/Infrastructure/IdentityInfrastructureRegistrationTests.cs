using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Infrastructure;

namespace Urban.UnitTests.Identity.Infrastructure;

public class IdentityInfrastructureRegistrationTests
{
    [Fact]
    public void TheEmailNotifierIsScoped_BecauseItHoldsAnHttpClientThatMustFollowDnsChanges()
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

        services.Single(d => d.ServiceType == typeof(IEmailVerificationNotifier)).Lifetime.Should().Be(ServiceLifetime.Scoped);
    }
}