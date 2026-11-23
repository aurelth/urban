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
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(audiences);

        await Task.CompletedTask;

        throw new NotImplementedException();
    }
}
