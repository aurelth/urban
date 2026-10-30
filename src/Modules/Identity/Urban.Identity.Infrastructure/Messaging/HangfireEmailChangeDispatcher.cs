using Hangfire;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Domain.Users;

namespace Urban.Identity.Infrastructure.Messaging;

/// <summary>Enfileira o envio do link de troca no Hangfire. A fila guarda só o id da conta e o endereço novo; o segredo nasce dentro do envio.</summary>
public sealed class HangfireEmailChangeDispatcher(IBackgroundJobClient client) : IEmailChangeDispatcher
{
    public Task EnqueueAsync(Guid userId, Email newEmail, CancellationToken cancellationToken)
    {
        string address = newEmail.Value;
        client.Enqueue<SendEmailChangeJob>(job => job.RunAsync(userId, address, CancellationToken.None));

        return Task.CompletedTask;
    }
}
