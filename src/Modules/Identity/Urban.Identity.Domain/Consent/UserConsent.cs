namespace Urban.Identity.Domain.Consent;

/// <summary>
/// Registro de que um usuário aceitou uma versão dos termos de uso e da política de privacidade. Só se acrescenta,
/// nunca se altera: o histórico de aceites é a prova de qual texto valia quando a pessoa concordou.
/// </summary>
public sealed class UserConsent
{
    public const int MaxVersionLength = 32;

    private UserConsent(Guid id, Guid userId, string version, DateTimeOffset acceptedAt)
    {
        Id = id;
        UserId = userId;
        Version = version;
        AcceptedAt = acceptedAt;
    }

    public Guid Id { get; }

    public Guid UserId { get; }

    public string Version { get; }

    public DateTimeOffset AcceptedAt { get; }

    public static UserConsent Record(Guid userId, string version, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(version.Length, MaxVersionLength);

        return new UserConsent(Guid.CreateVersion7(), userId, version, now);
    }
}