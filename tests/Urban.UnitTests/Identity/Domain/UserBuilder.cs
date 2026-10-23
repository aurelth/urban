using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;

namespace Urban.UnitTests.Identity.Domain;

/// <summary>Monta usuários em qualquer estado usando só a API pública do Domain (as mesmas transições do sistema real).</summary>
internal static class UserBuilder
{
    public static readonly DateTimeOffset Now = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    public static readonly Guid ActorId = Guid.CreateVersion7();

    public static User InStatus(AccountStatus status, UserRole role = UserRole.Resident)
    {
        Guid? condominiumId = role == UserRole.SuperAdmin ? null : Guid.CreateVersion7();
        User user = User.Register(Email.Create("ana@example.com").Value, role, condominiumId, Now).Value;

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
