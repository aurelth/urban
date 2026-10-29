using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Infrastructure;
using Urban.Condominium.Infrastructure.Persistence;
using Urban.Condominium.Infrastructure.Security;

namespace Urban.UnitTests.Condominium.Infrastructure;

public class CondominiumInfrastructureRegistrationTests
{
    private static string Key() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private static Dictionary<string, string?> ValidSettings() => new()
    {
        ["ConnectionStrings:Postgres"] = "Host=localhost;Database=unused;Username=unused;Password=unused",
        ["PersonalData:CurrentKeyId"] = "v1",
        ["PersonalData:EncryptionKeys:v1"] = Key(),
        ["PersonalData:HashKey"] = Key(),
    };

    [Fact]
    public void TheProtectorIsASingleton_BecauseItHoldsOnlyKeys()
    {
        ServiceDescriptor descriptor = Register(ValidSettings()).Single(d => d.ServiceType == typeof(IPersonalDataProtector));

        descriptor.ImplementationType.Should().Be<PersonalDataProtector>();
        descriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact]
    public void TheDbContextIsScoped_SoItFollowsTheRequestAndItsTenant()
    {
        Register(ValidSettings()).Single(d => d.ServiceType == typeof(CondominiumDbContext)).Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void TheSettingsAreReadFromTheEnvironmentStyleKeys()
    {
        Dictionary<string, string?> settings = ValidSettings();
        settings["PersonalData:EncryptionKeys:v2"] = Key();
        settings["PersonalData:CurrentKeyId"] = "v2";

        ServiceDescriptor descriptor = Register(settings).Single(d => d.ServiceType == typeof(PersonalDataOptions));

        PersonalDataOptions options = (PersonalDataOptions)descriptor.ImplementationInstance!;
        options.CurrentKeyId.Should().Be("v2");
        options.EncryptionKeys.Keys.Should().BeEquivalentTo("v1", "v2");
    }

    [Theory]
    [InlineData("PersonalData:EncryptionKeys:v1", "*'v1'*")]
    [InlineData("PersonalData:HashKey", "*PersonalData:HashKey*")]
    [InlineData("PersonalData:CurrentKeyId", "*PersonalData:CurrentKeyId*")]
    public void RefusesToStartWhenAPersonalDataSettingIsMissingOrInvalid(string setting, string message)
    {
        Dictionary<string, string?> settings = ValidSettings();
        settings[setting] = setting.EndsWith("CurrentKeyId", StringComparison.Ordinal) ? "v7" : "curta";

        Action register = () => Register(settings);

        register.Should().Throw<InvalidOperationException>().WithMessage(message);
    }

    [Fact]
    public void RefusesToStartWithoutAnyPersonalDataSetting()
    {
        Action register = () => Register(new Dictionary<string, string?> { ["ConnectionStrings:Postgres"] = "Host=localhost" });

        register.Should().Throw<InvalidOperationException>().WithMessage("*PersonalData*");
    }

    [Fact]
    public void RefusesToStartWithoutTheDatabaseConnection()
    {
        Dictionary<string, string?> settings = ValidSettings();
        settings.Remove("ConnectionStrings:Postgres");

        Action register = () => Register(settings);

        register.Should().Throw<InvalidOperationException>().WithMessage("*'Postgres'*");
    }

    private static ServiceCollection Register(Dictionary<string, string?> settings)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        ServiceCollection services = new();
        services.AddCondominiumInfrastructure(configuration);

        return services;
    }
}
