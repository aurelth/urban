using System.Net;
using FluentAssertions;
using Urban.Api.Http;

namespace Urban.IntegrationTests.Api;

public class RateLimitingConfigurationTests
{
    private static readonly RateLimitRule ValidRule = new() { PermitLimit = 5, Window = TimeSpan.FromMinutes(1) };

    [Fact]
    public void Accepts_a_rule_for_every_route()
    {
        Action validate = () => WithRule("Register", ValidRule).Validate();

        validate.Should().NotThrow();
    }

    [Theory]
    [InlineData("Register", 0)]
    [InlineData("ConfirmEmail", -1)]
    [InlineData("Login", 0)]
    [InlineData("ForgotPassword", 0)]
    [InlineData("ResetPassword", -1)]
    [InlineData("Refresh", 0)]
    [InlineData("Logout", -1)]
    [InlineData("Me", 0)]
    [InlineData("Terms", -1)]
    [InlineData("CondominiumRegistration", 0)]
    [InlineData("PostalCodeLookup", -1)]
    [InlineData("CondominiumProfile", 0)]
    [InlineData("SensitiveChange", -1)]
    public void Rejects_a_route_without_permits(string route, int permits)
    {
        RateLimitingOptions options = WithRule(route, new() { PermitLimit = permits, Window = TimeSpan.FromMinutes(1) });

        Action validate = options.Validate;

        validate.Should().Throw<InvalidOperationException>().WithMessage($"*RateLimiting:{route}:PermitLimit*");
    }

    [Theory]
    [InlineData("Register")]
    [InlineData("ConfirmEmail")]
    [InlineData("Login")]
    [InlineData("ForgotPassword")]
    [InlineData("ResetPassword")]
    [InlineData("Refresh")]
    [InlineData("Logout")]
    [InlineData("Me")]
    [InlineData("Terms")]
    [InlineData("CondominiumRegistration")]
    [InlineData("PostalCodeLookup")]
    [InlineData("CondominiumProfile")]
    [InlineData("SensitiveChange")]
    public void Rejects_a_route_without_a_window(string route)
    {
        RateLimitingOptions options = WithRule(route, new() { PermitLimit = 5, Window = TimeSpan.Zero });

        Action validate = options.Validate;

        validate.Should().Throw<InvalidOperationException>().WithMessage($"*RateLimiting:{route}:Window*");
    }

    [Fact]
    public void Rejects_a_missing_section_because_nothing_is_configured()
    {
        Action validate = new RateLimitingOptions().Validate;

        validate.Should().Throw<InvalidOperationException>().WithMessage("*RateLimiting:Register:PermitLimit*");
    }

    [Fact]
    public void Trusted_proxies_accept_networks_and_hops()
    {
        TrustedProxiesOptions options = new() { Networks = ["10.0.0.0/8", "fd00::/8"], Hops = 2 };

        options.Validate();
        IReadOnlyList<IPNetwork> networks = options.ParseNetworks();

        networks.Should().HaveCount(2);
        networks[0].Contains(IPAddress.Parse("10.1.2.3")).Should().BeTrue();
        networks[0].Contains(IPAddress.Parse("11.1.2.3")).Should().BeFalse();
    }

    [Fact]
    public void Trusted_proxies_require_at_least_one_network()
    {
        Action validate = new TrustedProxiesOptions { Networks = [], Hops = 2 }.Validate;

        validate.Should().Throw<InvalidOperationException>().WithMessage("*TrustedProxies:Networks*");
    }

    [Theory]
    [InlineData("nao-e-uma-rede")]
    [InlineData("10.0.0.0")]
    [InlineData("10.0.0.0/33")]
    public void Trusted_proxies_reject_a_network_that_is_not_in_CIDR_notation(string network)
    {
        Action validate = new TrustedProxiesOptions { Networks = ["10.0.0.0/8", network], Hops = 2 }.Validate;

        validate.Should().Throw<InvalidOperationException>().WithMessage($"*'{network}'*TrustedProxies:Networks*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(TrustedProxiesOptions.MaxHops + 1)]
    public void Trusted_proxies_reject_a_hop_count_outside_the_range(int hops)
    {
        Action validate = new TrustedProxiesOptions { Networks = ["10.0.0.0/8"], Hops = hops }.Validate;

        validate.Should().Throw<InvalidOperationException>().WithMessage("*TrustedProxies:Hops*");
    }

    [Theory]
    [InlineData("RateLimiting:Register:PermitLimit", "0", "RateLimiting:Register:PermitLimit")]
    [InlineData("RateLimiting:Login:Window", "00:00:00", "RateLimiting:Login:Window")]
    [InlineData("RateLimiting:ForgotPassword:PermitLimit", "0", "RateLimiting:ForgotPassword:PermitLimit")]
    [InlineData("RateLimiting:ResetPassword:Window", "00:00:00", "RateLimiting:ResetPassword:Window")]
    [InlineData("RateLimiting:Refresh:PermitLimit", "0", "RateLimiting:Refresh:PermitLimit")]
    [InlineData("RateLimiting:Logout:Window", "00:00:00", "RateLimiting:Logout:Window")]
    [InlineData("RateLimiting:Me:PermitLimit", "0", "RateLimiting:Me:PermitLimit")]
    [InlineData("RateLimiting:Terms:Window", "00:00:00", "RateLimiting:Terms:Window")]
    [InlineData("RateLimiting:CondominiumRegistration:PermitLimit", "0", "RateLimiting:CondominiumRegistration:PermitLimit")]
    [InlineData("RateLimiting:PostalCodeLookup:Window", "00:00:00", "RateLimiting:PostalCodeLookup:Window")]
    [InlineData("TrustedProxies:Hops", "0", "TrustedProxies:Hops")]
    [InlineData("TrustedProxies:Networks:0", "nao-e-uma-rede", "TrustedProxies:Networks")]
    public void Refuses_to_start_with_an_invalid_setting(string key, string value, string reported)
    {
        using ApiFactory factory = new() { Settings = new Dictionary<string, string?> { [key] = value } };

        Action start = () => factory.CreateClient();

        start.Should().Throw<InvalidOperationException>().WithMessage($"*{reported}*");
    }

    private static RateLimitingOptions WithRule(string route, RateLimitRule rule) => new()
    {
        Register = route == "Register" ? rule : ValidRule,
        ConfirmEmail = route == "ConfirmEmail" ? rule : ValidRule,
        Login = route == "Login" ? rule : ValidRule,
        ForgotPassword = route == "ForgotPassword" ? rule : ValidRule,
        ResetPassword = route == "ResetPassword" ? rule : ValidRule,
        Refresh = route == "Refresh" ? rule : ValidRule,
        Logout = route == "Logout" ? rule : ValidRule,
        Me = route == "Me" ? rule : ValidRule,
        Terms = route == "Terms" ? rule : ValidRule,
        CondominiumRegistration = route == "CondominiumRegistration" ? rule : ValidRule,
        PostalCodeLookup = route == "PostalCodeLookup" ? rule : ValidRule,
        CondominiumProfile = route == "CondominiumProfile" ? rule : ValidRule,
        SensitiveChange = route == "SensitiveChange" ? rule : ValidRule,
    };
}