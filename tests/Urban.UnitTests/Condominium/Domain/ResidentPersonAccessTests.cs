using FluentAssertions;
using Urban.Condominium.Domain.People;
using Urban.SharedKernel;
using Urban.TestSupport.Condominium;

namespace Urban.UnitTests.Condominium.Domain;

/// <summary>O morador nasce sem conta; o convite o liga a um usuário do Identity, uma única vez.</summary>
public class ResidentPersonAccessTests
{
    [Fact]
    public void AttachUser_LinksTheAccountToAResidentWithoutOne()
    {
        Person person = ResidentBuilders.Resident();
        Guid userId = Guid.CreateVersion7();

        Result result = person.AttachUser(userId);

        result.IsSuccess.Should().BeTrue();
        person.UserId.Should().Be(userId);
    }

    [Fact]
    public void AttachUser_WithAnEmptyId_Fails()
    {
        Person person = ResidentBuilders.Resident();

        Result result = person.AttachUser(Guid.Empty);

        result.Error.Should().Be(PersonErrors.UserRequired);
        person.UserId.Should().BeNull();
    }

    [Fact]
    public void AttachUser_ForSomeoneWhoAlreadyHasAnAccount_FailsAndKeepsTheFirstLink()
    {
        Person person = ResidentBuilders.Resident();
        Guid first = Guid.CreateVersion7();
        person.AttachUser(first);

        Result result = person.AttachUser(Guid.CreateVersion7());

        result.Error.Should().Be(PersonErrors.UserAlreadyAttached);
        person.UserId.Should().Be(first);
    }

    [Fact]
    public void TheErrorsOfTheInvitationHaveStableCodes()
    {
        PersonErrors.UserAlreadyAttached.Code.Should().Be("condominium.person.user_already_attached");
        ResidentErrors.EmailRequiredForInvitation.Code.Should().Be("condominium.resident.email_required");
        ResidentErrors.EmailLocked.Code.Should().Be("condominium.resident.email_locked");
    }
}
