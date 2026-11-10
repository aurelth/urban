using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Urban.Api.Http;
using Urban.Condominium.Application.PostalCodes;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.Contacts;
using Urban.Condominium.Domain.Documents;
using Urban.Condominium.Domain.People;
using Urban.Condominium.Domain.Units;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Consumption;
using Urban.Finance.Domain.Expenses;
using Urban.Finance.Domain.Months;
using Urban.Identity.Application.Authentication;
using Urban.Identity.Application.Authentication.Login;
using Urban.Identity.Application.Authentication.Refresh;
using Urban.Identity.Application.Bootstrap;
using Urban.Identity.Domain.Consent;
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
        { BootstrapErrors.EmailTakenByAnotherRole.Code, 409 },
        { EmailVerificationTokenErrors.Invalid.Code, 400 },
        { EmailVerificationTokenErrors.Expired.Code, 400 },
        { PasswordResetTokenErrors.Invalid.Code, 400 },
        { PasswordResetTokenErrors.Expired.Code, 400 },
        { ConsentErrors.Required.Code, 400 },
        { ConsentErrors.Outdated.Code, 400 },
        { SessionErrors.Unauthenticated.Code, 401 },
        { SessionErrors.StoreUnavailable.Code, 503 },
        { UserErrors.NotManager.Code, 403 },
        { UserErrors.CondominiumAlreadyAssigned.Code, 409 },
        { UserErrors.CondominiumRequired.Code, 422 },
        { DisplayName.Invalid.Code, 400 },
        { Cpf.Invalid.Code, 400 },
        { Cnpj.Invalid.Code, 400 },
        { PostalCode.Invalid.Code, 400 },
        { PhoneNumber.Invalid.Code, 400 },
        { PersonName.Invalid.Code, 400 },
        { BirthDate.Invalid.Code, 400 },
        { BirthDate.UnderAge.Code, 400 },
        { PersonErrors.MobileRequired.Code, 400 },
        { CondominiumErrors.NameInvalid.Code, 400 },
        { Address.StreetInvalid.Code, 400 },
        { Address.NumberInvalid.Code, 400 },
        { Address.ComplementInvalid.Code, 400 },
        { Address.NeighborhoodInvalid.Code, 400 },
        { Address.CityInvalid.Code, 400 },
        { Address.StateInvalid.Code, 400 },
        { CondominiumErrors.CnpjAlreadyRegistered.Code, 409 },
        { CondominiumErrors.ManagerAlreadyRegistered.Code, 409 },
        { CondominiumErrors.ManagerRequired.Code, 422 },
        { PersonErrors.CondominiumRequired.Code, 422 },
        { PersonErrors.UserRequired.Code, 422 },
        { PostalCodeErrors.NotFound.Code, 404 },
        { PostalCodeErrors.LookupUnavailable.Code, 503 },
        { UserErrors.PasswordIncorrect.Code, 403 },
        { UserErrors.PasswordUnchanged.Code, 400 },
        { UserErrors.EmailUnchanged.Code, 400 },
        { EmailChangeTokenErrors.Invalid.Code, 400 },
        { EmailChangeTokenErrors.Expired.Code, 400 },
        { CondominiumErrors.NotFound.Code, 404 },
        { CondominiumErrors.ConcurrencyConflict.Code, 409 },
        { CondominiumErrors.VersionRequired.Code, 400 },
        { PersonErrors.CpfAlreadyRegistered.Code, 409 },
        { CondominiumErrors.ApportionmentMethodInvalid.Code, 400 },
        { UnitErrors.NumberInvalid.Code, 400 },
        { UnitErrors.BlockInvalid.Code, 400 },
        { UnitErrors.TypeInvalid.Code, 400 },
        { UnitErrors.IdealFractionInvalid.Code, 400 },
        { UnitErrors.AreaInvalid.Code, 400 },
        { UnitErrors.CondominiumRequired.Code, 422 },
        { UnitErrors.VersionRequired.Code, 400 },
        { UnitErrors.PageInvalid.Code, 400 },
        { UnitErrors.BatchSizeInvalid.Code, 400 },
        { UnitErrors.NotFound.Code, 404 },
        { UnitErrors.IdentifierAlreadyExists.Code, 409 },
        { UnitErrors.Deactivated.Code, 409 },
        { UnitErrors.AlreadyDeactivated.Code, 409 },
        { UnitErrors.AlreadyActive.Code, 409 },
        { UnitErrors.FractionSumExceeded.Code, 422 },
        { FinanceErrors.CondominiumNotFound.Code, 404 },
        { FinanceErrors.VersionRequired.Code, 400 },
        { FinanceErrors.ConcurrencyConflict.Code, 409 },
        { MonthErrors.Invalid.Code, 400 },
        { MonthErrors.TooFarAhead.Code, 422 },
        { ExpenseGroupErrors.NameInvalid.Code, 400 },
        { ExpenseGroupErrors.CondominiumRequired.Code, 422 },
        { ExpenseGroupErrors.NotFound.Code, 404 },
        { ExpenseGroupErrors.NameAlreadyExists.Code, 409 },
        { ExpenseErrors.DescriptionInvalid.Code, 400 },
        { ExpenseErrors.SupplierInvalid.Code, 400 },
        { ExpenseErrors.AmountInvalid.Code, 400 },
        { ExpenseErrors.InstallmentCountInvalid.Code, 400 },
        { ExpenseErrors.CurrentInstallmentInvalid.Code, 400 },
        { ExpenseErrors.GroupRequired.Code, 400 },
        { ExpenseErrors.EndBeforeStart.Code, 400 },
        { ExpenseErrors.ChangeBeforeStart.Code, 400 },
        { ExpenseErrors.KindInvalid.Code, 400 },
        { ExpenseErrors.CondominiumRequired.Code, 422 },
        { ExpenseErrors.NotFixed.Code, 422 },
        { ExpenseErrors.NotInstallment.Code, 422 },
        { ExpenseErrors.EndNotAllowed.Code, 422 },
        { ExpenseErrors.NotDueInMonth.Code, 422 },
        { ExpenseErrors.NotFound.Code, 404 },
        { ExpenseErrors.InstallmentNotFound.Code, 404 },
        { ExpenseErrors.EntryNotFound.Code, 404 },
        { ExpenseErrors.DueDayInvalid.Code, 400 },
        { ExpenseErrors.StatusInvalid.Code, 400 },
        { ExpenseErrors.AmountRequiredToPay.Code, 422 },
        { ClosingErrors.TotalInvalid.Code, 422 },
        { ClosingErrors.NoWeights.Code, 422 },
        { ClosingErrors.MonthNotOpen.Code, 422 },
        { ClosingErrors.AmountsPending.Code, 422 },
        { ClosingErrors.NothingToCharge.Code, 422 },
        { ClosingErrors.DueDateInvalid.Code, 400 },
        { ClosingErrors.AlreadyClosed.Code, 409 },
        { ClosingErrors.NotClosed.Code, 409 },
        { ClosingErrors.MonthIsClosed.Code, 409 },
        { ClosingErrors.ReadingsPending(1, 1).Code, 422 },
        { ConsumptionErrors.BandsInvalid.Code, 400 },
        { ConsumptionErrors.GasInvalid.Code, 400 },
        { ConsumptionErrors.SewagePercentInvalid.Code, 400 },
        { ConsumptionErrors.ReserveFundPercentInvalid.Code, 400 },
        { ConsumptionErrors.ReadingInvalid.Code, 400 },
        { ConsumptionErrors.CondominiumRequired.Code, 422 },
        { ConsumptionErrors.UnitRequired.Code, 422 },
        { ConsumptionErrors.TariffNotFound.Code, 404 },
        { ConsumptionErrors.UnitNotFound.Code, 404 },
        { ConsumptionErrors.ServiceInvalid.Code, 400 },
        { CondominiumErrors.ApportionmentNotReady.Code, 422 },
    };

    [Fact]
    public void Every_error_declared_by_the_identity_condominium_and_finance_modules_has_a_status_code()
    {
        Assembly[] assemblies =
        [
            typeof(Email).Assembly,
            Urban.Identity.Application.AssemblyReference.Assembly,
            typeof(Cpf).Assembly,
            Urban.Condominium.Application.AssemblyReference.Assembly,
            typeof(FinanceErrors).Assembly,
            Urban.Finance.Application.AssemblyReference.Assembly,
        ];

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
    public void Problem_carries_the_error_data_under_the_data_field()
    {
        Dictionary<string, object?> data = new() { ["reason"] = "fractions_total", ["fractionTotal"] = 0.05m };
        Error error = CondominiumErrors.ApportionmentNotReady with { Data = data };

        ProblemHttpResult result = (ProblemHttpResult)ErrorHttp.ToProblem(error);

        result.StatusCode.Should().Be(422);
        result.ProblemDetails.Extensions["data"].Should().BeSameAs(data);
        result.ProblemDetails.Extensions["code"].Should().Be("condominium.apportionment_not_ready");
    }

    [Fact]
    public void Problem_without_error_data_has_no_data_field()
    {
        ProblemHttpResult result = (ProblemHttpResult)ErrorHttp.ToProblem(LoginErrors.LockedOut);

        result.ProblemDetails.Extensions.Should().NotContainKey("data");
    }

    [Fact]
    public void Unmapped_error_becomes_500_without_leaking_details()
    {
        ProblemHttpResult result = (ProblemHttpResult)ErrorHttp.ToProblem(new Error("x.never_mapped", "detalhe interno"));

        result.StatusCode.Should().Be(500);
        result.ProblemDetails.Detail.Should().NotContain("detalhe interno");
    }
}