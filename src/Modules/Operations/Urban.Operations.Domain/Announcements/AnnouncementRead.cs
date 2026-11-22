namespace Urban.Operations.Domain.Announcements;

/// <summary>Uma conta que marcou o comunicado como lido. A chave é o par comunicado e conta: marcar de novo não cria outra linha.</summary>
public sealed class AnnouncementRead
{
    public AnnouncementRead(Guid announcementId, Guid userId, Guid condominiumId, DateTimeOffset readAt)
    {
        AnnouncementId = announcementId;
        UserId = userId;
        CondominiumId = condominiumId;
        ReadAt = readAt;
    }

    public Guid AnnouncementId { get; }

    public Guid UserId { get; }

    public Guid CondominiumId { get; }

    public DateTimeOffset ReadAt { get; }
}
