using Hangfire;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Domain.Users;

namespace Urban.Identity.Infrastructure.Messaging;

/// <summary>Tarefa em segundo plano que envia um aviso de segurança. Se o envio falha, a exceção sobe e o Hangfire tenta de novo.</summary>
public sealed class SendSecurityNoticeJob(ISecurityNoticeNotifier notifier)
{
    [AutomaticRetry(Attempts = 5, DelaysInSeconds = [60, 300, 900, 3600, 10800], OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public async Task RunAsync(SecurityNotice notice, string recipient, string? other, CancellationToken cancellationToken)
    {
        Email to = Email.Create(recipient).Value;
        Email? second = other is null ? null : Email.Create(other).Value;

        await notifier.SendAsync(notice, to, second, cancellationToken);
    }
}
