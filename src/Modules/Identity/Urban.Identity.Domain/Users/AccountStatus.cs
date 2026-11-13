namespace Urban.Identity.Domain.Users;

/// <summary>Estado da conta. Só muda pelas transições do <see cref="User"/>.</summary>
public enum AccountStatus
{
    PendingVerification = 0,
    Active = 1,
    Deactivated = 2,
    Suspended = 3,
    Deleted = 4,

    /// <summary>Pedido de cadastro de síndico aguardando o SuperAdmin; nada é enviado à pessoa até a aprovação.</summary>
    PendingApproval = 5,

    /// <summary>Pedido recusado. É final: novos pedidos com o mesmo e-mail são ignorados.</summary>
    Rejected = 6,
}
