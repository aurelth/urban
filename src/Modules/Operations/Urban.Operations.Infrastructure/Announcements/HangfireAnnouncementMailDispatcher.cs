using Hangfire;
using Urban.Operations.Application.Abstractions;

namespace Urban.Operations.Infrastructure.Announcements;

/// <summary>Enfileira os e-mails dos comunicados no Hangfire. É uma gravação rápida no banco da fila; o envio de verdade acontece fora da requisição.</summary>
public sealed class HangfireAnnouncementMailDispatcher(IBackgroundJobClient client) : IAnnouncementMailDispatcher
{
    public Task EnqueueFanOutAsync(Guid condominiumId, Guid announcementId, CancellationToken cancellationToken) =>
        throw new NotImplementedException($"Não implementado: {client.GetType().Name} {condominiumId} {announcementId} {cancellationToken}");

    public Task EnqueueDeliveryAsync(Guid condominiumId, Guid announcementId, Guid userId, CancellationToken cancellationToken) =>
        throw new NotImplementedException($"Não implementado: {client.GetType().Name} {condominiumId} {announcementId} {userId} {cancellationToken}");
}
