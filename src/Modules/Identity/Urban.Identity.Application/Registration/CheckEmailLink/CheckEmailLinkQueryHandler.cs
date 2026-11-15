using MediatR;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Tokens;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Identity.Application.Registration.CheckEmailLink;

/// <summary>
/// Diz se o link de confirmação (cadastro ou convite) ainda serve, sem gastá-lo e sem mudar nada: a tela o consulta ao abrir, para avisar
/// de um link vencido ou já usado antes de a pessoa digitar a senha. As respostas são as mesmas da confirmação (inválido ou vencido), e
/// quem decide de verdade continua sendo a confirmação, que confere tudo de novo.
/// </summary>
public sealed class CheckEmailLinkQueryHandler(
    IEmailVerifications verifications,
    IUserAccounts accounts,
    ISecretGenerator secrets,
    IClock clock) : IRequestHandler<CheckEmailLinkQuery, Result>
{
    public async Task<Result> Handle(CheckEmailLinkQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return Result.Failure(EmailVerificationTokenErrors.Invalid);
        }

        EmailVerificationToken? token = await verifications.FindByHashAsync(secrets.Hash(request.Token), cancellationToken);
        if (token is null)
        {
            return Result.Failure(EmailVerificationTokenErrors.Invalid);
        }

        Result usable = token.Check(clock.UtcNow);
        if (usable.IsFailure)
        {
            return usable;
        }

        User? user = await accounts.FindByIdAsync(token.UserId, cancellationToken);

        return user?.Status == AccountStatus.PendingVerification
            ? Result.Success()
            : Result.Failure(EmailVerificationTokenErrors.Invalid);
    }
}
