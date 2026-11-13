using MediatR;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Identity.Application.Registration.RegisterManager;

/// <summary>
/// Pedido de cadastro do síndico: e-mail, nome e nome do condomínio. Nada é enviado à pessoa até o SuperAdmin aprovar; quem recebe
/// um aviso é o dono. A resposta é a mesma para e-mail novo, pedido em análise, pedido recusado ou conta já ativa, então ninguém
/// descobre quais e-mails têm conta nem o resultado de um pedido. Só quem já foi aprovado e ainda não confirmou o e-mail recebe novo
/// link por aqui. O SuperAdmin pendente é tratado pelo bootstrap, e este endpoint público não pode encher a caixa de entrada dele.
/// </summary>
public sealed class RegisterManagerCommandHandler(
    IUserAccounts accounts,
    IEmailVerifications verifications,
    IEmailVerificationDispatcher linkDispatcher,
    IRegistrationRequestDispatcher requestDispatcher,
    IClock clock) : IRequestHandler<RegisterManagerCommand, Result>
{
    public async Task<Result> Handle(RegisterManagerCommand request, CancellationToken cancellationToken)
    {
        Result<Email> email = Email.Create(request.Email);
        if (email.IsFailure)
        {
            return Result.Failure(email.Error);
        }

        Result<DisplayName> name = DisplayName.Create(request.Name);
        if (name.IsFailure)
        {
            return Result.Failure(name.Error);
        }

        Result<RequestedCondominiumName> condominium = RequestedCondominiumName.Create(request.CondominiumName);
        if (condominium.IsFailure)
        {
            return Result.Failure(condominium.Error);
        }

        User? existing = await accounts.FindByEmailAsync(email.Value, cancellationToken);
        DateTimeOffset now = clock.UtcNow;

        if (existing is null)
        {
            await RequestAccessAsync(User.RequestManagerAccess(email.Value, name.Value, condominium.Value, now), cancellationToken);
        }
        else if (existing.Role == UserRole.Manager && existing.Status == AccountStatus.PendingVerification)
        {
            await verifications.RequestLinkUnlessRecentAsync(linkDispatcher, existing.Id, now, cancellationToken);
        }

        return Result.Success();
    }

    private async Task RequestAccessAsync(User user, CancellationToken cancellationToken)
    {
        if (await verifications.TryRegisterAsync(user, cancellationToken))
        {
            await requestDispatcher.EnqueueAsync(user.Id, cancellationToken);
        }
    }
}