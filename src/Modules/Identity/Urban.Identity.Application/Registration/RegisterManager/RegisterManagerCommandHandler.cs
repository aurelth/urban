using MediatR;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Identity.Application.Registration.RegisterManager;

/// <summary>
/// Cadastro da síndica: só o e-mail. A resposta é a mesma para e-mail novo, conta pendente ou conta já ativa, então
/// ninguém descobre quais e-mails têm conta. A senha é escolhida depois, ao confirmar o e-mail. O link é emitido e enviado em segundo plano.
/// Só contas de síndico recebem novo link por aqui: o SuperAdmin pendente é tratado pelo bootstrap, e este endpoint público não pode ser usado para encher a caixa de entrada dele.
/// </summary>
public sealed class RegisterManagerCommandHandler(
    IUserAccounts accounts,
    IEmailVerifications verifications,
    IEmailVerificationDispatcher dispatcher,
    IClock clock) : IRequestHandler<RegisterManagerCommand, Result>
{
    public async Task<Result> Handle(RegisterManagerCommand request, CancellationToken cancellationToken)
    {
        Result<Email> email = Email.Create(request.Email);
        if (email.IsFailure)
        {
            return Result.Failure(email.Error);
        }

        User? existing = await accounts.FindByEmailAsync(email.Value, cancellationToken);
        DateTimeOffset now = clock.UtcNow;

        if (existing is null)
        {
            await RegisterNewAsync(email.Value, now, cancellationToken);
        }
        else if (existing.Role == UserRole.Manager && existing.Status == AccountStatus.PendingVerification)
        {
            await verifications.RequestLinkUnlessRecentAsync(dispatcher, existing.Id, now, cancellationToken);
        }

        return Result.Success();
    }

    private async Task RegisterNewAsync(Email email, DateTimeOffset now, CancellationToken cancellationToken)
    {
        User user = User.Register(email, UserRole.Manager, null, now).Value;

        if (await verifications.TryRegisterAsync(user, cancellationToken))
        {
            await dispatcher.EnqueueAsync(user.Id, cancellationToken);
        }
    }
}