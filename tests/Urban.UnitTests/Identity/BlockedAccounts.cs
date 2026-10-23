using Urban.Identity.Domain.Users;

namespace Urban.UnitTests.Identity;

/// <summary>Os estados de conta que não podem abrir sessão e o erro esperado de cada um. Compartilhado por Login e Refresh.</summary>
public static class BlockedAccounts
{
    public static TheoryData<AccountStatus, string> States => new()
    {
        { AccountStatus.PendingVerification, UserErrors.EmailNotVerified.Code },
        { AccountStatus.Deactivated, UserErrors.AccountDeactivated.Code },
        { AccountStatus.Suspended, UserErrors.AccountSuspended.Code },
        { AccountStatus.Deleted, UserErrors.AccountDeleted.Code },
    };
}
