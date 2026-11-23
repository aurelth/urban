using Hangfire;
using MediatR;
using Urban.Operations.Application.Announcements;
using Urban.SharedKernel.Abstractions;

namespace Urban.Operations.Infrastructure.Announcements;

/// <summary>
/// Tarefa em segundo plano que envia o e-mail de um comunicado a uma conta. Cada conta tem a sua tarefa: a falha do provedor para um
/// endereço é tentada de novo (5 depois da primeira: 60 s, 5 min, 15 min, 1 h, 3 h) sem repetir o envio de quem já recebeu.
/// </summary>
public sealed class SendAnnouncementEmailJob(ITenantScope tenant, ISender sender)
{
    [AutomaticRetry(Attempts = 5, DelaysInSeconds = [60, 300, 900, 3600, 10800], OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public async Task RunAsync(Guid condominiumId, Guid announcementId, Guid userId, CancellationToken cancellationToken)
    {
        tenant.Enter(condominiumId);
        await sender.Send(new SendAnnouncementEmailCommand(announcementId, userId), cancellationToken);
    }
}
