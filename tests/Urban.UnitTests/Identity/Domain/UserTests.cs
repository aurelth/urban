using FluentAssertions;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.TestSupport.Identity;

namespace Urban.UnitTests.Identity.Domain;

public class UserTests
{
    public enum Transition
    {
        ConfirmEmail,
        Deactivate,
        Activate,
        Suspend,
        Reactivate,
        Delete,
    }

    // Especificação da máquina de estados: (transição, estado de origem) -> estado de destino.
    private static readonly Dictionary<(Transition, AccountStatus), AccountStatus> Allowed = new()
    {
        [(Transition.ConfirmEmail, AccountStatus.PendingVerification)] = AccountStatus.Active,
        [(Transition.Deactivate, AccountStatus.Active)] = AccountStatus.Deactivated,
        [(Transition.Activate, AccountStatus.Deactivated)] = AccountStatus.Active,
        [(Transition.Suspend, AccountStatus.Active)] = AccountStatus.Suspended,
        [(Transition.Suspend, AccountStatus.Deactivated)] = AccountStatus.Suspended,
        [(Transition.Reactivate, AccountStatus.Suspended)] = AccountStatus.Active,
        [(Transition.Delete, AccountStatus.PendingVerification)] = AccountStatus.Deleted,
        [(Transition.Delete, AccountStatus.Active)] = AccountStatus.Deleted,
        [(Transition.Delete, AccountStatus.Deactivated)] = AccountStatus.Deleted,
        [(Transition.Delete, AccountStatus.Suspended)] = AccountStatus.Deleted,
    };

    public static TheoryData<Transition, AccountStatus, AccountStatus> AllowedTransitions
    {
        get
        {
            TheoryData<Transition, AccountStatus, AccountStatus> data = [];
            foreach (((Transition transition, AccountStatus from), AccountStatus to) in Allowed)
            {
                data.Add(transition, from, to);
            }

            return data;
        }
    }

    public static TheoryData<Transition, AccountStatus> ForbiddenTransitions
    {
        get
        {
            TheoryData<Transition, AccountStatus> data = [];
            foreach (Transition transition in Enum.GetValues<Transition>())
            {
                foreach (AccountStatus from in Enum.GetValues<AccountStatus>())
                {
                    if (!Allowed.ContainsKey((transition, from)))
                    {
                        data.Add(transition, from);
                    }
                }
            }

            return data;
        }
    }

    [Fact]
    public void Register_Manager_StartsPendingVerificationWithoutCondominium()
    {
        Result<User> result = User.Register(Email.Create("ana@example.com").Value, UserRole.Manager, null, UserBuilder.Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(AccountStatus.PendingVerification);
        result.Value.CondominiumId.Should().BeNull();
        result.Value.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Register_ResidentWithCondominium_Succeeds()
    {
        Guid condominiumId = Guid.CreateVersion7();

        Result<User> result = User.Register(Email.Create("bia@example.com").Value, UserRole.Resident, condominiumId, UserBuilder.Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.CondominiumId.Should().Be(condominiumId);
    }

    [Fact]
    public void Register_ResidentWithoutCondominium_Fails()
    {
        Result<User> result = User.Register(Email.Create("bia@example.com").Value, UserRole.Resident, null, UserBuilder.Now);

        result.Error.Should().Be(UserErrors.CondominiumMismatch);
    }

    [Fact]
    public void Register_SuperAdminWithCondominium_Fails()
    {
        Result<User> result = User.Register(Email.Create("dono@example.com").Value, UserRole.SuperAdmin, Guid.CreateVersion7(), UserBuilder.Now);

        result.Error.Should().Be(UserErrors.CondominiumMismatch);
    }

    [Fact]
    public void Register_StartsWithoutADisplayName()
    {
        User.Register(Email.Create("ana@example.com").Value, UserRole.Manager, null, UserBuilder.Now).Value
            .DisplayName.Should().BeNull();
    }

    [Fact]
    public void AssignCondominium_ToAManagerWithoutOne_SetsTheCondominiumAndTheDisplayName()
    {
        User manager = UserBuilder.ManagerWithoutCondominium();
        Guid condominiumId = Guid.CreateVersion7();
        DisplayName name = DisplayName.Create("Ana").Value;

        Result result = manager.AssignCondominium(condominiumId, name);

        result.IsSuccess.Should().BeTrue();
        manager.CondominiumId.Should().Be(condominiumId);
        manager.DisplayName.Should().Be(name);
    }

    [Fact]
    public void AssignCondominium_WhenTheManagerAlreadyHasOne_FailsAndKeepsEverything()
    {
        User manager = UserBuilder.InStatus(AccountStatus.Active, UserRole.Manager);
        Guid current = manager.CondominiumId!.Value;

        Result result = manager.AssignCondominium(Guid.CreateVersion7(), DisplayName.Create("Ana").Value);

        result.Error.Should().Be(UserErrors.CondominiumAlreadyAssigned);
        UserErrors.CondominiumAlreadyAssigned.Code.Should().Be("identity.user.condominium_already_assigned");
        manager.CondominiumId.Should().Be(current);
        manager.DisplayName.Should().BeNull();
    }

    [Theory]
    [InlineData(UserRole.Resident)]
    [InlineData(UserRole.SuperAdmin)]
    public void AssignCondominium_ForAnyRoleButManager_FailsAsNotManager(UserRole role)
    {
        User user = UserBuilder.InStatus(AccountStatus.Active, role);
        Guid? before = user.CondominiumId;

        Result result = user.AssignCondominium(Guid.CreateVersion7(), DisplayName.Create("Ana").Value);

        result.Error.Should().Be(UserErrors.NotManager);
        UserErrors.NotManager.Code.Should().Be("identity.user.not_manager");
        user.CondominiumId.Should().Be(before);
        user.DisplayName.Should().BeNull();
    }

    [Fact]
    public void AssignCondominium_WithAnEmptyCondominiumId_Fails()
    {
        User manager = UserBuilder.ManagerWithoutCondominium();

        Result result = manager.AssignCondominium(Guid.Empty, DisplayName.Create("Ana").Value);

        result.Error.Should().Be(UserErrors.CondominiumRequired);
        UserErrors.CondominiumRequired.Code.Should().Be("identity.user.condominium_required");
        manager.CondominiumId.Should().BeNull();
        manager.DisplayName.Should().BeNull();
    }

    [Fact]
    public void AssignCondominium_DoesNotTouchTheStatusOfTheAccount()
    {
        User manager = UserBuilder.ManagerWithoutCondominium();
        DateTimeOffset changedAt = manager.StatusChangedAt;

        manager.AssignCondominium(Guid.CreateVersion7(), DisplayName.Create("Ana").Value);

        manager.Status.Should().Be(AccountStatus.Active);
        manager.StatusChangedAt.Should().Be(changedAt);
    }

    [Theory]
    [MemberData(nameof(AllowedTransitions))]
    public void Transition_FromAnAllowedStatus_MovesToTheTargetStatus(Transition transition, AccountStatus from, AccountStatus to)
    {
        User user = UserBuilder.InStatus(from);

        Result result = Apply(user, transition);

        result.IsSuccess.Should().BeTrue();
        user.Status.Should().Be(to);
    }

    [Theory]
    [MemberData(nameof(ForbiddenTransitions))]
    public void Transition_FromAForbiddenStatus_FailsAndKeepsTheStatus(Transition transition, AccountStatus from)
    {
        User user = UserBuilder.InStatus(from);

        Result result = Apply(user, transition);

        result.Error.Should().Be(UserErrors.InvalidTransition);
        user.Status.Should().Be(from);
    }

    [Fact]
    public void Suspend_RecordsWhoWhenAndWhy()
    {
        User user = UserBuilder.InStatus(AccountStatus.Active);
        DateTimeOffset later = UserBuilder.Now.AddDays(1);

        user.Suspend(UserBuilder.ActorId, "  Uso indevido da plataforma  ", later);

        user.StatusChangedBy.Should().Be(UserBuilder.ActorId);
        user.StatusChangedAt.Should().Be(later);
        user.StatusReason.Should().Be("Uso indevido da plataforma");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Suspend_WithoutReason_FailsAndKeepsTheStatus(string? reason)
    {
        User user = UserBuilder.InStatus(AccountStatus.Active);

        Result result = user.Suspend(UserBuilder.ActorId, reason, UserBuilder.Now);

        result.Error.Should().Be(UserErrors.ReasonRequired);
        user.Status.Should().Be(AccountStatus.Active);
    }

    [Fact]
    public void Reactivate_ClearsTheSuspensionReason()
    {
        User user = UserBuilder.InStatus(AccountStatus.Suspended);

        user.Reactivate(UserBuilder.ActorId, UserBuilder.Now);

        user.StatusReason.Should().BeNull();
    }

    [Fact]
    public void Delete_AnonymizesTheEmailAndRecordsTheDeletion()
    {
        User user = UserBuilder.InStatus(AccountStatus.Active);
        string originalEmail = user.Email.Value;
        DateTimeOffset later = UserBuilder.Now.AddDays(2);

        user.Delete(UserBuilder.ActorId, later);

        user.DeletedAt.Should().Be(later);
        user.Email.Value.Should().NotBe(originalEmail);
        user.Email.Value.Should().Contain(user.Id.ToString("N"));
        Email.Create(user.Email.Value).IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(AccountStatus.Active, "")]
    [InlineData(AccountStatus.PendingVerification, "identity.user.email_not_verified")]
    [InlineData(AccountStatus.Deactivated, "identity.user.account_deactivated")]
    [InlineData(AccountStatus.Suspended, "identity.user.account_suspended")]
    [InlineData(AccountStatus.Deleted, "identity.user.account_deleted")]
    public void CheckCanSignIn_DependsOnTheAccountStatus(AccountStatus status, string expectedErrorCode)
    {
        User user = UserBuilder.InStatus(status);

        Result result = user.CheckCanSignIn();

        result.Error.Code.Should().Be(expectedErrorCode);
    }

    private static Result Apply(User user, Transition transition) => transition switch
    {
        Transition.ConfirmEmail => user.ConfirmEmail(UserBuilder.Now),
        Transition.Deactivate => user.Deactivate(UserBuilder.ActorId, UserBuilder.Now),
        Transition.Activate => user.Activate(UserBuilder.ActorId, UserBuilder.Now),
        Transition.Suspend => user.Suspend(UserBuilder.ActorId, "Motivo de teste", UserBuilder.Now),
        Transition.Reactivate => user.Reactivate(UserBuilder.ActorId, UserBuilder.Now),
        Transition.Delete => user.Delete(UserBuilder.ActorId, UserBuilder.Now),
        _ => throw new ArgumentOutOfRangeException(nameof(transition)),
    };
}
