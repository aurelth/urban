using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Urban.Api.Http;
using Urban.SharedKernel.Abstractions;

namespace Urban.IntegrationTests.Api;

public sealed class HttpTenantContextTests
{
    [Fact]
    public void The_tenant_is_the_condominium_claim_of_the_access_token()
    {
        Guid condominiumId = Guid.CreateVersion7();

        ITenantContext tenant = ContextWith(new Claim("condominium_id", condominiumId.ToString()));

        tenant.CondominiumId.Should().Be(condominiumId);
    }

    [Fact]
    public void Without_the_claim_there_is_no_tenant()
    {
        ContextWith(new Claim("sub", Guid.CreateVersion7().ToString())).CondominiumId.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void A_claim_that_is_not_a_real_condominium_id_means_no_tenant(string value)
    {
        ContextWith(new Claim("condominium_id", value)).CondominiumId.Should().BeNull();
    }

    [Fact]
    public void Outside_a_request_there_is_no_tenant()
    {
        new HttpTenantContext(new HttpContextAccessor()).CondominiumId.Should().BeNull();
    }

    [Fact]
    public void An_anonymous_request_has_no_tenant()
    {
        HttpContextAccessor accessor = new() { HttpContext = new DefaultHttpContext() };

        new HttpTenantContext(accessor).CondominiumId.Should().BeNull();
    }

    [Fact]
    public void The_tenant_follows_the_current_request_and_is_not_cached_across_requests()
    {
        HttpContextAccessor accessor = new() { HttpContext = RequestWith(new Claim("condominium_id", Guid.CreateVersion7().ToString())) };
        HttpTenantContext tenant = new(accessor);
        Guid second = Guid.CreateVersion7();

        accessor.HttpContext = RequestWith(new Claim("condominium_id", second.ToString()));

        tenant.CondominiumId.Should().Be(second);
    }

    [Fact]
    public void A_background_job_chooses_its_condominium_without_any_request()
    {
        Guid condominiumId = Guid.CreateVersion7();
        HttpTenantContext tenant = new(new HttpContextAccessor());

        ((ITenantScope)tenant).Enter(condominiumId);

        ((ITenantContext)tenant).CondominiumId.Should().Be(condominiumId);
    }

    [Fact]
    public void The_condominium_a_job_chose_wins_over_the_token_of_the_request()
    {
        Guid chosen = Guid.CreateVersion7();
        HttpTenantContext tenant = ContextWith(new Claim("condominium_id", Guid.CreateVersion7().ToString()));

        tenant.Enter(chosen);

        tenant.CondominiumId.Should().Be(chosen);
    }

    [Fact]
    public void A_job_cannot_choose_the_empty_condominium()
    {
        HttpTenantContext tenant = new(new HttpContextAccessor());

        Action enter = () => tenant.Enter(Guid.Empty);

        enter.Should().Throw<ArgumentOutOfRangeException>();
        tenant.CondominiumId.Should().BeNull();
    }

    private static HttpTenantContext ContextWith(params Claim[] claims) =>
        new(new HttpContextAccessor { HttpContext = RequestWith(claims) });

    private static DefaultHttpContext RequestWith(params Claim[] claims) =>
        new() { User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test")) };
}
