using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;

namespace Urban.TestSupport.Identity;

/// <summary>Monta usuários em qualquer estado usando só a API pública do Domain (as mesmas transições do sistema real).</summary>
public static class UserBuilder
{
    public static readonly DateTimeOffset Now = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    public static readonly Guid ActorId = Guid.CreateVersion7();

    public static User InStatus(AccountStatus status, UserRole role = UserRole.Resident, string address = "ana@example.com")
    {
        Guid? condominiumId = role == UserRole.SuperAdmin ? null : Guid.CreateVersion7();
        User user = User.Register(Email.Create(address).Value, role, condominiumId, Now).Value;

        return MoveTo(user, status);
    }

    /// <summary>Síndico que já entrou no sistema, mas ainda não cadastrou o condomínio (e por isso ainda não tem nome de exibição).</summary>
    public static User ManagerWithoutCondominium(AccountStatus status = AccountStatus.Active, string address = "sindica@example.com")
    {
        User user = User.Register(Email.Create(address).Value, UserRole.Manager, null, Now).Value;

        return MoveTo(user, status);
    }

    /// <summary>Síndico que já cadastrou o condomínio e escolheu como prefere ser chamado.</summary>
    public static User EnrolledManager(string displayName, string address = "sindica@example.com")
    {
        User user = ManagerWithoutCondominium(address: address);
        user.AssignCondominium(Guid.CreateVersion7(), DisplayName.Create(displayName).Value);

        return user;
    }

    private static User MoveTo(User user, AccountStatus status)
    {
        switch (status)
        {
            case AccountStatus.Active:
                user.ConfirmEmail(Now);
                break;
            case AccountStatus.Deactivated:
                user.ConfirmEmail(Now);
                user.Deactivate(ActorId, Now);
                break;
            case AccountStatus.Suspended:
                user.ConfirmEmail(Now);
                user.Suspend(ActorId, "Motivo de teste", Now);
                break;
            case AccountStatus.Deleted:
                user.Delete(ActorId, Now);
                break;
        }

        return user;
    }
}
