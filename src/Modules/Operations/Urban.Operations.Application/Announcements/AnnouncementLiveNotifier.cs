using Microsoft.Extensions.Logging;
using Urban.Operations.Application.Abstractions;
using Urban.Operations.Domain;
using Urban.Operations.Domain.Announcements;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;

namespace Urban.Operations.Application.Announcements;

/// <summary>
/// Avisa em tempo real quem mora no alcance do comunicado que o mural mudou. Uma consulta ao condomínio diz quem mora hoje em todos os
/// alcances envolvidos (na edição, o de antes e o de depois: quem deixou de ser alcançado também precisa ver o aviso sumir); o síndico que
/// fez a mudança fica de fora. Roda só depois de a mudança ter sido gravada e nunca derruba o pedido: falha vira aviso no log.
/// </summary>
public sealed partial class AnnouncementLiveNotifier(
    IAnnouncementAudience directory,
    IAnnouncementEvents events,
    IClock clock,
    ILogger<AnnouncementLiveNotifier> logger)
{
    public async Task NotifyAsync(
        AnnouncementChangeKind kind,
        Guid announcementId,
        Guid authorId,
        IReadOnlyList<AnnouncementAudience> audiences,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audiences);

        try
        {
            List<AudienceTarget> targets = [.. audiences.Select(audience => new AudienceTarget(audience.Block, audience.UnitId))];

            IReadOnlyList<IReadOnlySet<Guid>> recipients = await directory.GetRecipientsAsync(targets, OperationsCalendar.Today(clock.UtcNow), cancellationToken);

            HashSet<Guid> users = [.. recipients.SelectMany(set => set)];
            users.Remove(authorId);

            if (users.Count == 0)
            {
                return;
            }

            await events.SendAsync(new AnnouncementChange(kind, announcementId), users, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogNotifyFailed(logger, exception, announcementId, kind);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Não foi possível avisar o mural em tempo real do comunicado {AnnouncementId} ({Kind}); quem estava com o mural aberto verá a mudança na próxima vez.")]
    private static partial void LogNotifyFailed(ILogger logger, Exception exception, Guid announcementId, AnnouncementChangeKind kind);
}
