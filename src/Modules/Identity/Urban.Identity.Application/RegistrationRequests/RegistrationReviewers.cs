using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;

namespace Urban.Identity.Application.RegistrationRequests;

/// <summary>
/// Quem pode ver e decidir pedidos de cadastro. O papel do token não basta: a conta é lida de novo, para que um SuperAdmin
/// suspenso ou apagado perca o acesso na hora, e não só quando o token expirar.
/// </summary>
internal static class RegistrationReviewers
{
    public static bool IsReviewer(User? actor) => actor is { Role: UserRole.SuperAdmin, Status: AccountStatus.Active };

    public static bool CanDecide(User reviewer, ManagementAction action, User target) =>
        AccessPolicy.CanManage(SubjectOf(reviewer), action, SubjectOf(target));

    private static AccessSubject SubjectOf(User user) => new(user.Id, user.Role, user.CondominiumId);
}
