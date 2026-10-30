using MediatR;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Domain.Tokens;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Identity.Application.EmailChange.ConfirmEmailChange;

/// <summary>
/// Quem abre o link prova que lê a caixa do endereço novo, e a conta passa a usá-lo. Depois de gravar, todas as sessões
/// terminam (o e-mail é a chave de entrada e de recuperação da conta) e o endereço ANTERIOR recebe um aviso, para o dono
/// real perceber uma troca que não pediu. Link desconhecido, usado, de conta que não está ativa ou de endereço que ficou
/// ocupado no meio do caminho têm o mesmo erro.
/// </summary>
public sealed class ConfirmEmailChangeCommandHandler(
    IUserAccounts accounts,
    IEmailChanges changes,
    ISecretGenerator secrets,
    IRefreshTokenRepository refreshTokens,
    ISessionRevocations sessions,
    ISecurityNoticeDispatcher notices,
    IClock clock) : IRequestHandler<ConfirmEmailChangeCommand, Result>
{
    public async Task<Result> Handle(ConfirmEmailChangeCommand request, CancellationToken cancellationToken)
    {
        EmailChangeToken? token = await changes.FindByHashAsync(secrets.Hash(request.Token), cancellationToken);
        if (token is null)
        {
            return Result.Failure(EmailChangeTokenErrors.Invalid);
        }

        DateTimeOffset now = clock.UtcNow;
        Result consumed = token.Consume(now);
        if (consumed.IsFailure)
        {
            return consumed;
        }

        User? user = await accounts.FindByIdAsync(token.UserId, cancellationToken);
        if (user is null || user.Status != AccountStatus.Active)
        {
            return Result.Failure(EmailChangeTokenErrors.Invalid);
        }

        Email previous = user.Email;
        if (user.ChangeEmail(token.NewEmail).IsFailure || !await changes.ConfirmAsync(token, user, cancellationToken))
        {
            return Result.Failure(EmailChangeTokenErrors.Invalid);
        }

        await refreshTokens.RevokeAllOfUserAsync(user.Id, now, cancellationToken);
        await sessions.RevokeAllOfUserAsync(user.Id, now, cancellationToken);
        await notices.EnqueueAsync(SecurityNotice.EmailChanged, previous, token.NewEmail, cancellationToken);

        return Result.Success();
    }
}
