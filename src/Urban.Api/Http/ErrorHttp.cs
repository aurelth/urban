using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.WebUtilities;
using Urban.Condominium.Application.PostalCodes;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.Contacts;
using Urban.Condominium.Domain.Documents;
using Urban.Condominium.Domain.People;
using Urban.Identity.Application.Authentication;
using Urban.Identity.Application.Authentication.Login;
using Urban.Identity.Application.Authentication.Refresh;
using Urban.Identity.Application.Bootstrap;
using Urban.Identity.Domain.Consent;
using Urban.Identity.Domain.Tokens;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;

namespace Urban.Api.Http;

public static class ErrorHttp
{
    private static readonly Dictionary<string, int> StatusByCode = new()
    {
        [LoginErrors.InvalidCredentials.Code] = StatusCodes.Status401Unauthorized,
        [RefreshErrors.InvalidSession.Code] = StatusCodes.Status401Unauthorized,
        [RefreshTokenErrors.Expired.Code] = StatusCodes.Status401Unauthorized,
        [RefreshTokenErrors.Reused.Code] = StatusCodes.Status401Unauthorized,
        [EmailVerificationTokenErrors.Invalid.Code] = StatusCodes.Status400BadRequest,
        [EmailVerificationTokenErrors.Expired.Code] = StatusCodes.Status400BadRequest,
        [PasswordResetTokenErrors.Invalid.Code] = StatusCodes.Status400BadRequest,
        [PasswordResetTokenErrors.Expired.Code] = StatusCodes.Status400BadRequest,
        [ConsentErrors.Required.Code] = StatusCodes.Status400BadRequest,
        [ConsentErrors.Outdated.Code] = StatusCodes.Status400BadRequest,
        [LoginErrors.LockedOut.Code] = StatusCodes.Status423Locked,
        [SessionErrors.Unauthenticated.Code] = StatusCodes.Status401Unauthorized,
        [SessionErrors.StoreUnavailable.Code] = StatusCodes.Status503ServiceUnavailable,

        [UserErrors.EmailNotVerified.Code] = StatusCodes.Status403Forbidden,
        [UserErrors.AccountDeactivated.Code] = StatusCodes.Status403Forbidden,
        [UserErrors.AccountSuspended.Code] = StatusCodes.Status403Forbidden,
        [UserErrors.AccountDeleted.Code] = StatusCodes.Status403Forbidden,

        [UserErrors.InvalidTransition.Code] = StatusCodes.Status409Conflict,
        [BootstrapErrors.EmailTakenByAnotherRole.Code] = StatusCodes.Status409Conflict,
        [UserErrors.ReasonRequired.Code] = StatusCodes.Status400BadRequest,
        [UserErrors.CondominiumMismatch.Code] = StatusCodes.Status422UnprocessableEntity,
        [UserErrors.CondominiumRequired.Code] = StatusCodes.Status422UnprocessableEntity,
        [UserErrors.NotManager.Code] = StatusCodes.Status403Forbidden,
        [UserErrors.CondominiumAlreadyAssigned.Code] = StatusCodes.Status409Conflict,
        [DisplayName.Invalid.Code] = StatusCodes.Status400BadRequest,
        [Email.Invalid.Code] = StatusCodes.Status400BadRequest,
        [PasswordPolicy.TooShort.Code] = StatusCodes.Status400BadRequest,
        [PasswordPolicy.TooLong.Code] = StatusCodes.Status400BadRequest,
        [PasswordPolicy.NeedsLetterAndDigit.Code] = StatusCodes.Status400BadRequest,

        [RequestErrors.Invalid.Code] = StatusCodes.Status400BadRequest,

        [Cpf.Invalid.Code] = StatusCodes.Status400BadRequest,
        [Cnpj.Invalid.Code] = StatusCodes.Status400BadRequest,
        [PostalCode.Invalid.Code] = StatusCodes.Status400BadRequest,
        [PhoneNumber.Invalid.Code] = StatusCodes.Status400BadRequest,
        [PersonName.Invalid.Code] = StatusCodes.Status400BadRequest,
        [BirthDate.Invalid.Code] = StatusCodes.Status400BadRequest,
        [BirthDate.UnderAge.Code] = StatusCodes.Status400BadRequest,
        [PersonErrors.MobileRequired.Code] = StatusCodes.Status400BadRequest,
        [CondominiumErrors.NameInvalid.Code] = StatusCodes.Status400BadRequest,
        [Address.StreetInvalid.Code] = StatusCodes.Status400BadRequest,
        [Address.NumberInvalid.Code] = StatusCodes.Status400BadRequest,
        [Address.ComplementInvalid.Code] = StatusCodes.Status400BadRequest,
        [Address.NeighborhoodInvalid.Code] = StatusCodes.Status400BadRequest,
        [Address.CityInvalid.Code] = StatusCodes.Status400BadRequest,
        [Address.StateInvalid.Code] = StatusCodes.Status400BadRequest,
        [CondominiumErrors.CnpjAlreadyRegistered.Code] = StatusCodes.Status409Conflict,
        [CondominiumErrors.ManagerAlreadyRegistered.Code] = StatusCodes.Status409Conflict,
        [CondominiumErrors.ManagerRequired.Code] = StatusCodes.Status422UnprocessableEntity,
        [PersonErrors.CondominiumRequired.Code] = StatusCodes.Status422UnprocessableEntity,
        [PersonErrors.UserRequired.Code] = StatusCodes.Status422UnprocessableEntity,
        [UserErrors.PasswordIncorrect.Code] = StatusCodes.Status403Forbidden,
        [UserErrors.PasswordUnchanged.Code] = StatusCodes.Status400BadRequest,
        [UserErrors.EmailUnchanged.Code] = StatusCodes.Status400BadRequest,
        [EmailChangeTokenErrors.Invalid.Code] = StatusCodes.Status400BadRequest,
        [EmailChangeTokenErrors.Expired.Code] = StatusCodes.Status400BadRequest,
        [CondominiumErrors.NotFound.Code] = StatusCodes.Status404NotFound,
        [CondominiumErrors.ConcurrencyConflict.Code] = StatusCodes.Status409Conflict,
        [CondominiumErrors.VersionRequired.Code] = StatusCodes.Status400BadRequest,
        [PersonErrors.CpfAlreadyRegistered.Code] = StatusCodes.Status409Conflict,
        [PostalCodeErrors.NotFound.Code] = StatusCodes.Status404NotFound,
        [PostalCodeErrors.LookupUnavailable.Code] = StatusCodes.Status503ServiceUnavailable,
    };

    public static bool TryGetStatusCode(Error error, [NotNullWhen(true)] out int status) =>
        StatusByCode.TryGetValue(error.Code, out status);

    public static IResult ToProblem(Error error)
    {
        if (!TryGetStatusCode(error, out int status))
        {
            return Results.Problem(
                detail: "Erro inesperado. Tente novamente em instantes",
                statusCode: StatusCodes.Status500InternalServerError,
                title: ReasonPhrases.GetReasonPhrase(StatusCodes.Status500InternalServerError));
        }

        return Results.Problem(
            detail: error.Description,
            statusCode: status,
            title: ReasonPhrases.GetReasonPhrase(status),
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
    }
}