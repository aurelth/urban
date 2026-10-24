using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Urban.Api.Http;
using Urban.Identity.Application.Authentication;
using Urban.Identity.Application.Authentication.Login;
using Urban.Identity.Application.Authentication.Refresh;
using Urban.Identity.Domain.Tokens;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;

namespace Urban.IntegrationTests.Api;

public sealed class ErrorHttpMappingTests
{
    public static TheoryData<string, int> ExpectedStatuses => new()
    {
        { LoginErrors.InvalidCredentials.Code, 401 },
        { LoginErrors.LockedOut.Code, 423 },
        { RefreshErrors.InvalidSession.Code, 401 },
        { RefreshTokenErrors.Expired.Code, 401 },
        { RefreshTokenErrors.Reused.Code, 401 },
        { UserErrors.EmailNotVerified.Code, 403 },
        { UserErrors.AccountDeactivated.Code, 403 },
        { UserErrors.AccountSuspended.Code, 403 },
        { UserErrors.AccountDeleted.Code, 403 },
        { UserErrors.InvalidTransition.Code, 409 },
        { SessionErrors.Unauthenticated.Code, 401 },
        { SessionErrors.StoreUnavailable.Code, 503 },
    };

    [Fact]
    public void Every_error_declared_by_the_identity_module_has_a_status_code()
    {
        Assembly[] assemblies = [typeof(Email).Assembly, Urban.Identity.Application.AssemblyReference.Assembly];

        Error[] errors = assemblies
            .SelectMany(a => a.GetExportedTypes())
            .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(f => f.FieldType == typeof(Error))
            .Select(f => (Error)f.GetValue(null)!)
            .ToArray();

        errors.Should().NotBeEmpty();
        errors.Where(e => !ErrorHttp.TryGetStatusCode(e, out _)).Select(e => e.Code).Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(ExpectedStatuses))]
    public void Maps_known_errors_to_the_agreed_status(string code, int status)
    {
        ErrorHttp.TryGetStatusCode(new Error(code, "x"), out int actual).Should().BeTrue();

        actual.Should().Be(status);
    }

    [Fact]
    public void Problem_carries_the_stable_code_and_the_user_message()
    {
        ProblemHttpResult result = (ProblemHttpResult)ErrorHttp.ToProblem(LoginErrors.LockedOut);

        result.StatusCode.Should().Be(423);
        result.ProblemDetails.Detail.Should().Be(LoginErrors.LockedOut.Description);
        result.ProblemDetails.Extensions["code"].Should().Be("identity.login.locked_out");
    }

    [Fact]
    public void Unmapped_error_becomes_500_without_leaking_details()
    {
        ProblemHttpResult result = (ProblemHttpResult)ErrorHttp.ToProblem(new Error("x.never_mapped", "detalhe interno"));

        result.StatusCode.Should().Be(500);
        result.ProblemDetails.Detail.Should().NotContain("detalhe interno");
    }
}