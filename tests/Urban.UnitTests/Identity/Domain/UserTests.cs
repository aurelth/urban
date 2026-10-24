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
