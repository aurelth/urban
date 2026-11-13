using Urban.Identity.Domain.Users;

namespace Urban.Identity.Application.Administration;

public static class ManagerStatuses
{
    /// <summary>
    /// Os estados em que uma conta de síndico aparece na lista do administrador. Pedidos em análise e recusados têm tela própria,
    /// e conta excluída é anônima.
    /// </summary>
    public static readonly IReadOnlyList<AccountStatus> Listed =
    [
        AccountStatus.Active,
        AccountStatus.Suspended,
        AccountStatus.Deactivated,
        AccountStatus.PendingVerification,
    ];
}
