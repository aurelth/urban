using MediatR;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Identity.Application.PasswordReset.ForgotPassword;

/// <summary>
/// Pedido de redefinição: só o e-mail. A resposta é a mesma para e-mail sem conta, conta que não pode entrar e conta ativa,
/// então ninguém descobre quais e-mails têm conta. Só conta ativa recebe o link, que é emitido e enviado em segundo plano.
/// </summary>
public sealed class ForgotPasswordCommandHandler(
    IUserAccounts accounts,
    IPasswordResets resets,
    IPasswordResetDispatcher dispatcher,
    IClock clock) : IRequestHandler<ForgotPasswordCommand, Result>
{
    public async Task<Result> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        Result<Email> email = Email.Create(request.Email);
        if (email.IsFailure)
        {
            return Result.Failure(email.Error);
        }

        User? user = await accounts.FindByEmailAsync(email.Value, cancellationToken);
        if (user is null || user.Status != AccountStatus.Active)
        {
            return Result.Success();
        }

        if (!await resets.HasRecentLinkAsync(user.Id, clock.UtcNow, cancellationToken))
        {
            await dispatcher.EnqueueAsync(user.Id, cancellationToken);
        }

        return Result.Success();
    }
}