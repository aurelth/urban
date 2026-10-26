namespace Urban.Identity.Domain.Users;

/// <summary>
/// Credencial de acesso de um usuário: o hash da senha e a contagem de falhas que decide o bloqueio. Fica separada do
/// <see cref="User"/> para o hash nunca viajar junto com os dados da conta. Quem calcula o hash é a Infrastructure.
/// </summary>
public sealed class UserCredential
{
    public const int MaxFailedAttempts = 5;

    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private UserCredential(Guid userId, string passwordHash)
    {
        UserId = userId;
        PasswordHash = passwordHash;
    }

    public Guid UserId { get; }

    public string PasswordHash { get; private set; }

    public int FailedAttempts { get; private set; }

    public DateTimeOffset? LockoutEnd { get; private set; }

    public static UserCredential Create(Guid userId, string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        return new UserCredential(userId, passwordHash);
    }

    public bool IsLockedOut(DateTimeOffset now) => LockoutEnd is not null && now < LockoutEnd;

    /// <summary>Conta uma senha errada. A quinta falha bloqueia por 15 minutos e zera a contagem; durante o bloqueio nada muda.</summary>
    public void RegisterFailure(DateTimeOffset now)
    {
        if (IsLockedOut(now))
        {
            return;
        }

        LockoutEnd = null;
        FailedAttempts++;

        if (FailedAttempts >= MaxFailedAttempts)
        {
            FailedAttempts = 0;
            LockoutEnd = now + LockoutDuration;
        }
    }

    /// <summary>Senha correta: zera a contagem e limpa um bloqueio que já terminou.</summary>
    public void RegisterSuccess() => ClearLockout();

    /// <summary>Troca a senha depois de uma redefinição por e-mail. Quem provou ter a caixa de e-mail também sai do bloqueio.</summary>
    public void ChangePassword(string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        PasswordHash = passwordHash;
        ClearLockout();
    }

    private void ClearLockout()
    {
        FailedAttempts = 0;
        LockoutEnd = null;
    }
}