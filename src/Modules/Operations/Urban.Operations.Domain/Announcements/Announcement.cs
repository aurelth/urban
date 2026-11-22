using Urban.SharedKernel;

namespace Urban.Operations.Domain.Announcements;

/// <summary>
/// Um comunicado do síndico aos moradores: título, texto, categoria e a quem se dirige. Quem leu fica em <see cref="AnnouncementRead"/>,
/// uma linha por conta. Editar o comunicado não zera quem já leu.
/// </summary>
public sealed class Announcement
{
    public const int TitleMaxLength = 120;
    public const int BodyMaxLength = 5000;

    // Usado só pelo EF Core ao ler do banco.
    private Announcement()
    {
        Title = null!;
        Body = null!;
    }

    public Guid Id { get; }

    public Guid CondominiumId { get; }

    public string Title { get; private set; }

    public string Body { get; private set; }

    public AnnouncementCategory Category { get; private set; }

    public AnnouncementScope Scope { get; private set; }

    public string? Block { get; private set; }

    public Guid? UnitId { get; private set; }

    /// <summary>A conta do síndico que escreveu.</summary>
    public Guid CreatedBy { get; }

    public DateTimeOffset CreatedAt { get; }

    /// <summary>Quando foi editado pela última vez; nulo se nunca foi.</summary>
    public DateTimeOffset? UpdatedAt { get; private set; }

    public AnnouncementAudience Audience => AnnouncementAudience.From(Scope, Block, UnitId);

    public static Result<Announcement> Create(
        Guid condominiumId,
        string? title,
        string? body,
        AnnouncementCategory category,
        AnnouncementAudience audience,
        Guid createdBy,
        DateTimeOffset now)
    {
        throw new NotImplementedException();
    }

    /// <summary>Troca tudo o que o síndico pode mudar. Quem já leu continua tendo lido.</summary>
    public Result Edit(string? title, string? body, AnnouncementCategory category, AnnouncementAudience audience, DateTimeOffset now)
    {
        throw new NotImplementedException();
    }
}
