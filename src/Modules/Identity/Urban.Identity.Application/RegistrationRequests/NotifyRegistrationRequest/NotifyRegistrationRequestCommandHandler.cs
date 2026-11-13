using MediatR;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;

namespace Urban.Identity.Application.RegistrationRequests.NotifyRegistrationRequest;

/// <summary>
/// Roda em segundo plano: avisa o dono de um pedido novo. Se o pedido já foi decidido quando a tarefa roda (a fila pode atrasar
/// ou tentar de novo), não há mais o que avisar. Se o envio falha, o erro sobe para o mecanismo de retentativa.
/// </summary>
public sealed class NotifyRegistrationRequestCommandHandler(
    IUserAccounts accounts,
    IRegistrationRequestNotifier notifier) : IRequestHandler<NotifyRegistrationRequestCommand, Result>
{
    public async Task<Result> Handle(NotifyRegistrationRequestCommand request, CancellationToken cancellationToken)
    {
        User? user = await accounts.FindByIdAsync(request.UserId, cancellationToken);
        if (user is null || user.Status != AccountStatus.PendingApproval)
        {
            return Result.Success();
        }

        await notifier.NotifyAsync(RegistrationRequestSummary.From(user), cancellationToken);

        return Result.Success();
    }
}
