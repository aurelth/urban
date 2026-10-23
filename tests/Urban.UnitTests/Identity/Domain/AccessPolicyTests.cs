using FluentAssertions;
using Urban.Identity.Domain.Access;

namespace Urban.UnitTests.Identity.Domain;

public class AccessPolicyTests
{
    private static readonly Guid CondominiumA = Guid.NewGuid();
    private static readonly Guid CondominiumB = Guid.NewGuid();

    // Especificação em forma de tabela: tudo que não está aqui é proibido.
    private static readonly HashSet<(UserRole Actor, ManagementAction Action, UserRole Target)> Allowed =
    [
        (UserRole.SuperAdmin, ManagementAction.Create, UserRole.Manager),
        (UserRole.SuperAdmin, ManagementAction.Suspend, UserRole.Manager),
        (UserRole.SuperAdmin, ManagementAction.Suspend, UserRole.Resident),
        (UserRole.SuperAdmin, ManagementAction.Reactivate, UserRole.Manager),
        (UserRole.SuperAdmin, ManagementAction.Reactivate, UserRole.Resident),
        (UserRole.SuperAdmin, ManagementAction.Delete, UserRole.Manager),
        (UserRole.SuperAdmin, ManagementAction.Delete, UserRole.Resident),
        (UserRole.Manager, ManagementAction.Create, UserRole.Resident),
        (UserRole.Manager, ManagementAction.Deactivate, UserRole.Resident),
        (UserRole.Manager, ManagementAction.Activate, UserRole.Resident),
        (UserRole.Manager, ManagementAction.Delete, UserRole.Resident),
    ];

    public static TheoryData<UserRole, ManagementAction, UserRole> AllCombinations
    {
        get
        {
            TheoryData<UserRole, ManagementAction, UserRole> data = [];
            foreach (UserRole actor in Enum.GetValues<UserRole>())
            {
                foreach (ManagementAction action in Enum.GetValues<ManagementAction>())
                {
                    foreach (UserRole target in Enum.GetValues<UserRole>())
                    {
                        data.Add(actor, action, target);
                    }
                }
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(AllCombinations))]
    public void CanManage_InTheSameCondominium_FollowsTheMatrix(UserRole actorRole, ManagementAction action, UserRole targetRole)
    {
        AccessSubject actor = SubjectOf(actorRole, CondominiumA);
        AccessSubject target = SubjectOf(targetRole, CondominiumA);

        bool allowed = AccessPolicy.CanManage(actor, action, target);

        allowed.Should().Be(Allowed.Contains((actorRole, action, targetRole)));
    }

    [Theory]
    [InlineData(UserRole.SuperAdmin, ManagementAction.Suspend)]
    [InlineData(UserRole.Manager, ManagementAction.Deactivate)]
    [InlineData(UserRole.Manager, ManagementAction.Delete)]
    public void CanManage_ActingOnYourself_IsNeverAllowed(UserRole role, ManagementAction action)
    {
        AccessSubject subject = SubjectOf(role, CondominiumA);

        AccessPolicy.CanManage(subject, action, subject).Should().BeFalse();
    }

    [Fact]
    public void CanManage_ManagerOnResidentOfAnotherCondominium_IsDenied()
    {
        AccessSubject manager = SubjectOf(UserRole.Manager, CondominiumA);
        AccessSubject resident = SubjectOf(UserRole.Resident, CondominiumB);

        AccessPolicy.CanManage(manager, ManagementAction.Deactivate, resident).Should().BeFalse();
    }

    [Fact]
    public void CanManage_ManagerWithoutCondominium_IsDenied()
    {
        AccessSubject manager = SubjectOf(UserRole.Manager, null);
        AccessSubject resident = SubjectOf(UserRole.Resident, null);

        AccessPolicy.CanManage(manager, ManagementAction.Deactivate, resident).Should().BeFalse();
    }

    [Fact]
    public void CanManage_SuperAdminOnManagerOfAnyCondominium_IsAllowed()
    {
        AccessSubject superAdmin = SubjectOf(UserRole.SuperAdmin, null);
        AccessSubject manager = SubjectOf(UserRole.Manager, CondominiumB);

        AccessPolicy.CanManage(superAdmin, ManagementAction.Suspend, manager).Should().BeTrue();
    }

    private static AccessSubject SubjectOf(UserRole role, Guid? condominiumId) =>
        new(Guid.CreateVersion7(), role, role == UserRole.SuperAdmin ? null : condominiumId);
}
