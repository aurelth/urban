using Hangfire;
using Urban.Identity.Application.Abstractions;

namespace Urban.Identity.Infrastructure.Messaging;

/// <summary>Enfileira o envio do link no Hangfire. É uma gravação rápida no banco; o envio de verdade acontece fora da requisição.</summary>
public sealed class HangfireEmailVerificationDispatcher(IBackgroundJobClient client) : IEmailVerificationDispatcher
{
    public Task EnqueueAsync(Guid userId, CancellationToken cancellationToken)
    {
        client.Enqueue<SendEmailVerificationJob>(job => job.RunAsync(userId, CancellationToken.None));

        return Task.CompletedTask;
    }
}