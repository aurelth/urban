namespace Urban.Identity.Domain.Users;

/// <summary>Estado da conta. Só muda pelas transições do <see cref="User"/>.</summary>
public enum AccountStatus
{
    PendingVerification = 0,
    Active = 1,
    Deactivated = 2,
    Suspended = 3,
    Deleted = 4,
}
