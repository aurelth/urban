using FluentAssertions;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.TestSupport.Identity;

namespace Urban.UnitTests.Identity.Domain;

public class UserEditTests
{
    [Fact]
    public void ChangeDisplayName_ReplacesHowTheManagerIsCalled()
    {
        User manager = UserBuilder.EnrolledManager("Ana");

        manager.ChangeDisplayName(DisplayName.Create("Aninha").Value);

        manager.DisplayName!.Value.Should().Be("Aninha");
    }

    [Fact]
    public void ChangeEmail_ReplacesTheAddress_AndKeepsTheRestOfTheAccount()
    {
        User manager = UserBuilder.EnrolledManager("Ana");
        (Guid id, Guid? condominiumId, AccountStatus status) = (manager.Id, manager.CondominiumId, manager.Status);

        Result result = manager.ChangeEmail(Email.Create("nova@example.com").Value);

        result.IsSuccess.Should().BeTrue();
        manager.Email.Value.Should().Be("nova@example.com");
        (manager.Id, manager.CondominiumId, manager.Status).Should().Be((id, condominiumId, status));
    }

    [Fact]
    public void ChangeEmail_ToTheSameAddress_FailsAndChangesNothing()
    {
        User manager = UserBuilder.EnrolledManager("Ana", "sindica@example.com");

        Result result = manager.ChangeEmail(Email.Create("Sindica@Example.com").Value);

        result.Error.Should().Be(UserErrors.EmailUnchanged);
        UserErrors.EmailUnchanged.Code.Should().Be("identity.email.unchanged");
        manager.Email.Value.Should().Be("sindica@example.com");
    }

    [Fact]
    public void ThePasswordErrors_HaveStableCodesForTheClient()
    {
        UserErrors.PasswordIncorrect.Code.Should().Be("identity.password.incorrect");
        UserErrors.PasswordUnchanged.Code.Should().Be("identity.password.unchanged");
    }
}
