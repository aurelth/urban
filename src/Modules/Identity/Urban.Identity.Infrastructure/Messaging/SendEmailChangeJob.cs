using Hangfire;
using MediatR;
using Urban.Identity.Application.EmailChange.SendEmailChange;

namespace Urban.Identity.Infrastructure.Messaging;

/// <summary>
/// Tarefa em segundo plano que emite e envia o link de troca de e-mail. Mesma política de retentativa do link de redefinição:
/// a primeira espera (60 s) não pode ser menor que <c>EmailLinkToken.ResendInterval</c>, e a execução é serializada para a regra
/// de 1 e-mail por minuto continuar valendo com duas tarefas da mesma conta na fila.
/// </summary>
public sealed class SendEmailChangeJob(ISender sender)
{
    [AutomaticRetry(Attempts = 5, DelaysInSeconds = [60, 300, 900, 3600, 10800], OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    [DisableConcurrentExecution(timeoutInSeconds: 120)]
    public async Task RunAsync(Guid userId, string newEmail, CancellationToken cancellationToken)
    {
        await sender.Send(new SendEmailChangeCommand(userId, newEmail), cancellationToken);
    }
}
