using FluentAssertions;
using Moq;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.EmailChange.SendEmailChange;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Tokens;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.TestSupport.Identity;

namespace Urban.UnitTests.Identity.Application;

public class SendEmailChangeCommandHandlerTests
{
    private const string NewAddress = "nova@example.com";
    private static readonly DateTimeOffset Now = UserBuilder.Now;

    private readonly Mock<IUserAccounts> _accounts = new();
    private readonly Mock<IEmailChanges> _changes = new();
    private readonly Mock<ISecretGenerator> _secrets = new();
    private readonly Mock<IEmailChangeNotifier> _notifier = new();
    private readonly Mock<IClock> _clock = new();
    private readonly List<string> _calls = [];
    private readonly User _user = UserBuilder.EnrolledManager("Ana", "sindica@example.com");
    private readonly SendEmailChangeCommandHandler _handler;

    public SendEmailChangeCommandHandlerTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(Now);
        _secrets.Setup(s => s.Generate()).Returns(new GeneratedSecret("link-secret", "link-hash"));
        _accounts.Setup(a => a.FindByIdAsync(_user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_user);
        _changes
            .Setup(c => c.AddAsync(It.IsAny<EmailChangeToken>(), It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("stored"))
            .Returns(Task.CompletedTask);
        _notifier
            .Setup(n => n.SendLinkAsync(It.IsAny<Email>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("sent"))
            .Returns(Task.CompletedTask);
        _handler = new SendEmailChangeCommandHandler(_accounts.Object, _changes.Object, _secrets.Object, _notifier.Object, _clock.Object);
    }

    [Fact]
    public async Task Handle_StoresOnlyTheHashAndTheNewAddress_AndSendsTheSecretToTheNewAddress()
    {
        EmailChangeToken? added = null;
        _changes
            .Setup(c => c.AddAsync(It.IsAny<EmailChangeToken>(), It.IsAny<CancellationToken>()))
            .Callback<EmailChangeToken, CancellationToken>((t, _) => added = t)
            .Returns(Task.CompletedTask);

        Result result = await _handler.Handle(new SendEmailChangeCommand(_user.Id, NewAddress), default);

        result.IsSuccess.Should().BeTrue();
        added!.UserId.Should().Be(_user.Id);
        added.NewEmail.Value.Should().Be(NewAddress);
        added.TokenHash.Should().Be("link-hash");
        added.CreatedAt.Should().Be(Now);
        _notifier.Verify(n => n.SendLinkAsync(It.Is<Email>(e => e.Value == NewAddress), "link-secret", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_StoresTheLinkBeforeSendingIt()
    {
        await _handler.Handle(new SendEmailChangeCommand(_user.Id, NewAddress), default);

        _calls.Should().Equal("stored", "sent");
    }

    [Fact]
    public async Task Handle_WhenALinkWasIssuedJustNow_DoesNothing()
    {
        EmailChangeToken recent = EmailChangeToken.Issue(_user.Id, Email.Create(NewAddress).Value, "recent-hash", Now.AddSeconds(-20));
        _changes.Setup(c => c.FindLatestOfUserAsync(_user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(recent);

        Result result = await _handler.Handle(new SendEmailChangeCommand(_user.Id, NewAddress), default);

        result.IsSuccess.Should().BeTrue();
        _calls.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenTheAddressWasTakenInTheMeantime_DoesNothing()
    {
        User other = UserBuilder.InStatus(AccountStatus.Active, UserRole.Resident, NewAddress);
        _accounts.Setup(a => a.FindByEmailAsync(other.Email, It.IsAny<CancellationToken>())).ReturnsAsync(other);

        Result result = await _handler.Handle(new SendEmailChangeCommand(_user.Id, NewAddress), default);

        result.IsSuccess.Should().BeTrue();
        _calls.Should().BeEmpty();
    }

    [Theory]
    [InlineData(AccountStatus.PendingVerification)]
    [InlineData(AccountStatus.Deactivated)]
    [InlineData(AccountStatus.Suspended)]
    [InlineData(AccountStatus.Deleted)]
    public async Task Handle_WhenTheAccountIsNoLongerActive_DoesNothing(AccountStatus status)
    {
        User other = UserBuilder.InStatus(status, UserRole.Manager);
        _accounts.Setup(a => a.FindByIdAsync(other.Id, It.IsAny<CancellationToken>())).ReturnsAsync(other);

        Result result = await _handler.Handle(new SendEmailChangeCommand(other.Id, NewAddress), default);

        result.IsSuccess.Should().BeTrue();
        _calls.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ForAnUnknownUser_DoesNothing()
    {
        Guid userId = Guid.CreateVersion7();
        _accounts.Setup(a => a.FindByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        Result result = await _handler.Handle(new SendEmailChangeCommand(userId, NewAddress), default);

        result.IsSuccess.Should().BeTrue();
        _calls.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithAMalformedAddress_DoesNothing()
    {
        Result result = await _handler.Handle(new SendEmailChangeCommand(_user.Id, "sem-arroba"), default);

        result.IsSuccess.Should().BeTrue();
        _calls.Should().BeEmpty();
    }
}
