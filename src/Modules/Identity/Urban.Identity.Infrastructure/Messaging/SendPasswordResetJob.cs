using Hangfire;
using MediatR;
using Urban.Identity.Application.PasswordReset.SendPasswordReset;

namespace Urban.Identity.Infrastructure.Messaging;

/// <summary>
/// Tarefa em segundo plano que emite e envia o link de redefinição. A fila guarda só o id da conta.
/// Se o envio falha, a exceção sobe e o Hangfire tenta de novo: a primeira espera (60 s) não pode ser menor que
/// <c>EmailLinkToken.ResendInterval</c>, senão a tentativa seria barrada pelo link que ela mesma emitiu antes.
/// A execução é serializada para a regra de 1 e-mail por minuto continuar valendo com duas tarefas da mesma conta na fila.
/// </summary>
public sealed class SendPasswordResetJob(ISender sender)
{
    [AutomaticRetry(Attempts = 5, DelaysInSeconds = [60, 300, 900, 3600, 10800], OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    [DisableConcurrentExecution(timeoutInSeconds: 120)]
    public async Task RunAsync(Guid userId, CancellationToken cancellationToken)
    {
        await sender.Send(new SendPasswordResetCommand(userId), cancellationToken);
    }
}