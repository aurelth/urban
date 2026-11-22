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

    private Announcement(
        Guid condominiumId,
        string title,
        string body,
        AnnouncementCategory category,
        AnnouncementAudience audience,
        Guid createdBy,
        DateTimeOffset createdAt)
    {
        Id = Guid.CreateVersion7();
        CondominiumId = condominiumId;
        Title = title;
        Body = body;
        Category = category;
        Scope = audience.Scope;
        Block = audience.Block;
        UnitId = audience.UnitId;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
    }

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
        if (condominiumId == Guid.Empty)
        {
            return Result.Failure<Announcement>(AnnouncementErrors.CondominiumRequired);
        }

        if (createdBy == Guid.Empty)
        {
            return Result.Failure<Announcement>(AnnouncementErrors.AuthorRequired);
        }

        Result<(string Title, string Body)> text = Validate(title, body);

        return text.IsFailure
            ? Result.Failure<Announcement>(text.Error)
            : Result.Success(new Announcement(condominiumId, text.Value.Title, text.Value.Body, category, audience, createdBy, now));
    }

    /// <summary>Troca tudo o que o síndico pode mudar. Quem já leu continua tendo lido.</summary>
    public Result Edit(string? title, string? body, AnnouncementCategory category, AnnouncementAudience audience, DateTimeOffset now)
    {
        Result<(string Title, string Body)> text = Validate(title, body);

        if (text.IsFailure)
        {
            return Result.Failure(text.Error);
        }

        Title = text.Value.Title;
        Body = text.Value.Body;
        Category = category;
        Scope = audience.Scope;
        Block = audience.Block;
        UnitId = audience.UnitId;
        UpdatedAt = now;

        return Result.Success();
    }

    private static Result<(string Title, string Body)> Validate(string? title, string? body)
    {
        if (!TextRules.IsValidLine(title, TitleMaxLength, out string normalizedTitle))
        {
            return Result.Failure<(string, string)>(AnnouncementErrors.TitleInvalid);
        }

        return TextRules.IsValidParagraphs(body, BodyMaxLength, out string normalizedBody)
            ? Result.Success((normalizedTitle, normalizedBody))
            : Result.Failure<(string, string)>(AnnouncementErrors.BodyInvalid);
    }
}
