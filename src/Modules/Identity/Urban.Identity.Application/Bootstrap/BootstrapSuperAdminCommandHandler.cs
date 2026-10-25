using MediatR;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Identity.Application.Bootstrap;

/// <summary>
/// Garante a conta do SuperAdmin a partir do e-mail configurado, sem senha em lugar nenhum: a conta nasce pendente e o link de
/// ativação vai para esse e-mail, então só quem lê a caixa de entrada escolhe a senha. Pode rodar a cada partida: com a conta já
/// criada não faz nada, e com ela ainda pendente apenas pede outro link (no máximo um por minuto).
/// </summary>
public sealed class BootstrapSuperAdminCommandHandler(
    IUserAccounts accounts,
    IEmailVerifications verifications,
    IEmailVerificationDispatcher dispatcher,
    IClock clock) : IRequestHandler<BootstrapSuperAdminCommand, Result>
{
    public async Task<Result> Handle(BootstrapSuperAdminCommand request, CancellationToken cancellationToken)
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
            await CreateAsync(email.Value, now, cancellationToken);
            return Result.Success();
        }

        if (existing.Role != UserRole.SuperAdmin)
        {
            return Result.Failure(BootstrapErrors.EmailTakenByAnotherRole);
        }

        if (existing.Status == AccountStatus.PendingVerification)
        {
            await verifications.RequestLinkUnlessRecentAsync(dispatcher, existing.Id, now, cancellationToken);
        }

        return Result.Success();
    }

    private async Task CreateAsync(Email email, DateTimeOffset now, CancellationToken cancellationToken)
    {
        User user = User.Register(email, UserRole.SuperAdmin, null, now).Value;

        if (await verifications.TryRegisterAsync(user, cancellationToken))
        {
            await dispatcher.EnqueueAsync(user.Id, cancellationToken);
        }
    }
}