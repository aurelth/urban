using Urban.Operations.Domain.Announcements;

namespace Urban.Operations.Application.Abstractions;

/// <summary>Um comunicado lido do banco, com a versão que a edição e a exclusão precisam devolver.</summary>
public sealed record AnnouncementRecord(Announcement Announcement, string Version);

/// <summary>Uma página de comunicados, do mais novo ao mais velho, e quantos há no total.</summary>
public sealed record PagedAnnouncements(IReadOnlyList<AnnouncementRecord> Items, int Total);

/// <summary>Quem lê o mural: a conta e onde ela mora hoje (os blocos e as unidades dos vínculos em andamento).</summary>
public sealed record AnnouncementViewer(Guid UserId, IReadOnlyCollection<string> Blocks, IReadOnlyCollection<Guid> UnitIds);
