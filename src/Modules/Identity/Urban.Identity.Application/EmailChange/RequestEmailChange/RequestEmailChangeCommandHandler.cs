using MediatR;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.Authentication;
using Urban.Identity.Application.Authentication.Login;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;

namespace Urban.Identity.Application.EmailChange.RequestEmailChange;

/// <summary>
/// Pedido de troca de e-mail: confere a senha e enfileira o link para o endereço NOVO e um aviso para o endereço atual.
/// A conta só muda quando o link é aberto. A resposta é a mesma para endereço livre e para endereço que já tem conta, então
/// o pedido não revela quais e-mails estão cadastrados. Endereço inválido ou igual ao atual não gasta tentativa de senha.
/// </summary>
public sealed class RequestEmailChangeCommandHandler(
    IUserAccounts accounts,
    IEmailChangeDispatcher links,
    ISecurityNoticeDispatcher notices) : IRequestHandler<RequestEmailChangeCommand, Result>
{
    public async Task<Result> Handle(RequestEmailChangeCommand request, CancellationToken cancellationToken)
    {
        Result<Email> newEmail = Email.Create(request.NewEmail);
        if (newEmail.IsFailure)
        {
            return Result.Failure(newEmail.Error);
        }

        User? user = await accounts.FindByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure(SessionErrors.Unauthenticated);
        }

        Result canSignIn = user.CheckCanSignIn();
        if (canSignIn.IsFailure)
        {
            return canSignIn;
        }

        if (newEmail.Value == user.Email)
        {
            return Result.Failure(UserErrors.EmailUnchanged);
        }

        Result<User> confirmed = await accounts.CheckCredentialsAsync(user.Email, request.Password ?? string.Empty, cancellationToken);
        if (confirmed.IsFailure)
        {
            return Result.Failure(confirmed.Error == LoginErrors.LockedOut ? LoginErrors.LockedOut : UserErrors.PasswordIncorrect);
        }

        if (await accounts.FindByEmailAsync(newEmail.Value, cancellationToken) is not null)
        {
            return Result.Success();
        }

        await links.EnqueueAsync(user.Id, newEmail.Value, cancellationToken);
        await notices.EnqueueAsync(SecurityNotice.EmailChangeRequested, user.Email, newEmail.Value, cancellationToken);

        return Result.Success();
    }
}
