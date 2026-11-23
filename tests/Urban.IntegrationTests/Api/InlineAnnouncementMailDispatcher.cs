using Microsoft.Extensions.DependencyInjection;
using Urban.Operations.Application.Abstractions;
using Urban.Operations.Infrastructure.Announcements;
using Urban.SharedKernel.Abstractions;

namespace Urban.IntegrationTests.Api;

/// <summary>
/// Executa os e-mails dos comunicados na hora, como o Hangfire faria: cada tarefa num escopo novo (sem requisição, então o condomínio
/// vem do id que a fila leva) e engolindo a falha do provedor, que a fila trataria como retentativa. Os testes de API leem o e-mail
/// logo depois do pedido.
/// </summary>
public sealed class InlineAnnouncementMailDispatcher(IServiceScopeFactory scopes) : IAnnouncementMailDispatcher
{
    public async Task EnqueueFanOutAsync(Guid condominiumId, Guid announcementId, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopes.CreateAsyncScope();
        FanOutAnnouncementEmailsJob job = ActivatorUtilities.CreateInstance<FanOutAnnouncementEmailsJob>(scope.ServiceProvider);

        await job.RunAsync(condominiumId, announcementId, cancellationToken);
    }

    public async Task EnqueueDeliveryAsync(Guid condominiumId, Guid announcementId, Guid userId, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopes.CreateAsyncScope();
        SendAnnouncementEmailJob job = ActivatorUtilities.CreateInstance<SendAnnouncementEmailJob>(scope.ServiceProvider);

        try
        {
            await job.RunAsync(condominiumId, announcementId, userId, cancellationToken);
        }
        catch (EmailDeliveryException)
        {
            // O Hangfire tentaria de novo mais tarde.
        }
    }
}
