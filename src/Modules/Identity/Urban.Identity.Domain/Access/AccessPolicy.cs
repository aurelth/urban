namespace Urban.Identity.Domain.Access;

/// <summary>Decide quem pode gerir a conta de quem. Função pura: não consulta banco nem relógio.</summary>
public static class AccessPolicy
{
    private static readonly ManagementAction[] ManagerActions =
    [
        ManagementAction.Create,
        ManagementAction.Deactivate,
        ManagementAction.Activate,
        ManagementAction.Delete,
    ];

    public static bool CanManage(AccessSubject actor, ManagementAction action, AccessSubject target)
    {
        if (actor.UserId == target.UserId || target.Role == UserRole.SuperAdmin)
        {
            return false;
        }

        return actor.Role switch
        {
            UserRole.SuperAdmin => SuperAdminCan(action, target.Role),
            UserRole.Manager => target.Role == UserRole.Resident
                && ManagerActions.Contains(action)
                && SameCondominium(actor, target),
            _ => false,
        };
    }

    private static bool SuperAdminCan(ManagementAction action, UserRole targetRole) => action switch
    {
        ManagementAction.Create => targetRole == UserRole.Manager,
        ManagementAction.Suspend or ManagementAction.Reactivate or ManagementAction.Delete => true,
        _ => false,
    };

    private static bool SameCondominium(AccessSubject actor, AccessSubject target) =>
        actor.CondominiumId is not null && actor.CondominiumId == target.CondominiumId;
}
