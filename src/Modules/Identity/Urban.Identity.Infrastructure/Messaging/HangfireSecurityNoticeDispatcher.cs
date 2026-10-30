using Hangfire;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Domain.Users;

namespace Urban.Identity.Infrastructure.Messaging;

/// <summary>Enfileira o aviso de segurança no Hangfire. A fila guarda o tipo do aviso e os endereços; nunca senha nem link.</summary>
public sealed class HangfireSecurityNoticeDispatcher(IBackgroundJobClient client) : ISecurityNoticeDispatcher
{
    public Task EnqueueAsync(SecurityNotice notice, Email recipient, Email? other, CancellationToken cancellationToken)
    {
        string to = recipient.Value;
        string? otherAddress = other?.Value;
        client.Enqueue<SendSecurityNoticeJob>(job => job.RunAsync(notice, to, otherAddress, CancellationToken.None));

        return Task.CompletedTask;
    }
}
