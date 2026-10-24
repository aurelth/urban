using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.WebUtilities;
using Urban.Identity.Application.Authentication.Login;
using Urban.Identity.Application.Authentication.Refresh;
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
        [LoginErrors.LockedOut.Code] = StatusCodes.Status423Locked,

        [UserErrors.EmailNotVerified.Code] = StatusCodes.Status403Forbidden,
        [UserErrors.AccountDeactivated.Code] = StatusCodes.Status403Forbidden,
        [UserErrors.AccountSuspended.Code] = StatusCodes.Status403Forbidden,
        [UserErrors.AccountDeleted.Code] = StatusCodes.Status403Forbidden,

        [UserErrors.InvalidTransition.Code] = StatusCodes.Status409Conflict,
        [UserErrors.ReasonRequired.Code] = StatusCodes.Status400BadRequest,
        [UserErrors.CondominiumMismatch.Code] = StatusCodes.Status422UnprocessableEntity,
        [Email.Invalid.Code] = StatusCodes.Status400BadRequest,
        [PasswordPolicy.TooShort.Code] = StatusCodes.Status400BadRequest,
        [PasswordPolicy.TooLong.Code] = StatusCodes.Status400BadRequest,
        [PasswordPolicy.NeedsLetterAndDigit.Code] = StatusCodes.Status400BadRequest,
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