using Urban.Operations.Domain.Announcements;
using Urban.SharedKernel;

namespace Urban.Operations.Application.Abstractions;

/// <summary>Porta de persistência dos comunicados e das leituras, sempre dentro do condomínio da requisição (tenant).</summary>
public interface IAnnouncements
{
    Task<AnnouncementRecord?> FindAsync(Guid announcementId, CancellationToken cancellationToken);

    /// <summary>Todos os comunicados do condomínio, do mais novo ao mais velho, uma página por vez.</summary>
    Task<PagedAnnouncements> ListAsync(int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>
    /// Os comunicados que a conta vê no mural, do mais novo ao mais velho: os do condomínio inteiro, os dos blocos e os das unidades em que
    /// ela mora. Os que a própria conta escreveu ficam de fora.
    /// </summary>
    /// <param name="unreadOnly">Só os que a conta ainda não marcou como lidos.</param>
    Task<PagedAnnouncements> ListVisibleAsync(AnnouncementViewer viewer, bool unreadOnly, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>Quantos comunicados do mural a conta ainda não marcou como lidos.</summary>
    Task<int> CountUnreadAsync(AnnouncementViewer viewer, CancellationToken cancellationToken);

    /// <summary>As contas que leram cada comunicado, numa consulta só. Comunicado sem leitura fica de fora.</summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlySet<Guid>>> ListReadersAsync(IReadOnlyCollection<Guid> announcementIds, CancellationToken cancellationToken);

    /// <summary>Quando a conta leu cada um dos comunicados; o que ela não leu fica de fora.</summary>
    Task<IReadOnlyDictionary<Guid, DateTimeOffset>> ListReadAtAsync(Guid userId, IReadOnlyCollection<Guid> announcementIds, CancellationToken cancellationToken);

    /// <returns>A versão do novo comunicado.</returns>
    Task<Result<string>> AddAsync(Announcement announcement, CancellationToken cancellationToken);

    /// <returns>A nova versão, ou <see cref="Urban.Operations.Domain.OperationsErrors.ConcurrencyConflict"/> se outra pessoa mudou antes.</returns>
    Task<Result<string>> SaveAsync(AnnouncementRecord record, CancellationToken cancellationToken);

    /// <summary>Apaga o comunicado e, junto, as leituras dele. Falha com conflito se outra pessoa mudou antes, e com não encontrado se já foi apagado.</summary>
    Task<Result> DeleteAsync(AnnouncementRecord record, CancellationToken cancellationToken);

    /// <summary>Marca como lido pela conta. Marcar de novo é sucesso e não muda a data; comunicado que sumiu no meio do caminho é não encontrado.</summary>
    Task<Result> MarkReadAsync(Guid announcementId, Guid userId, DateTimeOffset now, CancellationToken cancellationToken);
}
