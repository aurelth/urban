using Hangfire;
using MediatR;
using Urban.Operations.Application.Announcements;
using Urban.SharedKernel.Abstractions;

namespace Urban.Operations.Infrastructure.Announcements;

/// <summary>
/// Tarefa em segundo plano que descobre quem deve receber o e-mail de um comunicado e abre um envio para cada conta. A fila guarda só
/// os ids. Sem requisição não há token, então a tarefa escolhe o condomínio antes de qualquer consulta. A retentativa é a dos outros
/// e-mails (5 depois da primeira: 60 s, 5 min, 15 min, 1 h, 3 h).
/// </summary>
public sealed class FanOutAnnouncementEmailsJob(ITenantScope tenant, ISender sender)
{
    [AutomaticRetry(Attempts = 5, DelaysInSeconds = [60, 300, 900, 3600, 10800], OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public Task RunAsync(Guid condominiumId, Guid announcementId, CancellationToken cancellationToken) =>
        throw new NotImplementedException($"Não implementado: {tenant.GetType().Name} {sender.GetType().Name} {condominiumId} {announcementId} {cancellationToken}");
}
