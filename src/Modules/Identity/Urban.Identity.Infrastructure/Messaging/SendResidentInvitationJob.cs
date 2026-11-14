using Hangfire;
using MediatR;
using Urban.Identity.Application.Invitations.SendResidentInvitation;

namespace Urban.Identity.Infrastructure.Messaging;

/// <summary>
/// Tarefa em segundo plano que emite e envia o convite do morador. A fila guarda só o id da conta. A política de retentativa é a
/// do e-mail de confirmação: a primeira espera (60 s) não pode ser menor que <c>EmailLinkToken.ResendInterval</c>, senão a
/// tentativa seria barrada pelo link que ela mesma emitiu antes, e a execução é serializada para valer 1 e-mail por minuto.
/// </summary>
public sealed class SendResidentInvitationJob(ISender sender)
{
    [AutomaticRetry(Attempts = 5, DelaysInSeconds = [60, 300, 900, 3600, 10800], OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    [DisableConcurrentExecution(timeoutInSeconds: 120)]
    public async Task RunAsync(Guid userId, CancellationToken cancellationToken)
    {
        await sender.Send(new SendResidentInvitationCommand(userId), cancellationToken);
    }
}
