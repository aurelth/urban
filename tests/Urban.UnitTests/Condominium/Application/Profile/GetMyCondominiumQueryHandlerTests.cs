using System.Text.Json;
using FluentAssertions;
using Moq;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Application.Profile;
using Urban.Condominium.Domain.Condominiums;
using Urban.SharedKernel;
using Urban.SharedKernel.Contracts;
using Urban.TestSupport.Condominium;

namespace Urban.UnitTests.Condominium.Application.Profile;

public class GetMyCondominiumQueryHandlerTests
{
    private readonly Mock<ICondominiumProfiles> _profiles = new();
    private readonly Mock<IManagerProfile> _profile = new();
    private readonly GetMyCondominiumQueryHandler _handler;

    public GetMyCondominiumQueryHandlerTests()
    {
        _handler = new GetMyCondominiumQueryHandler(_profiles.Object, _profile.Object);
    }

    [Fact]
    public async Task Handle_LooksTheRegistrationUpByTheUserOfTheToken()
    {
        ManagerCondominium registration = CondominiumBuilders.Registration();
        Guid userId = registration.Manager.UserId;
        _profiles.Setup(p => p.FindOfManagerAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(registration);

        Result<MyCondominium> result = await _handler.Handle(new GetMyCondominiumQuery(userId), default);

        result.IsSuccess.Should().BeTrue();
        _profiles.Verify(p => p.FindOfManagerAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
        _profile.Verify(p => p.GetDisplayNameAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ReturnsTheDataOfTheCondominiumAndOfTheManager_WithTheDisplayNameFromIdentity()
    {
        ManagerCondominium registration = CondominiumBuilders.Registration(version: "7.9");
        Given(registration, displayName: "Aninha");

        MyCondominium mine = (await _handler.Handle(new GetMyCondominiumQuery(registration.Manager.UserId), default)).Value;

        mine.CondominiumId.Should().Be(registration.Condominium.Id);
        mine.Version.Should().Be("7.9");
        mine.DisplayName.Should().Be("Aninha");
        mine.Condominium.Name.Should().Be("Residencial Vila Verde");
        mine.Condominium.Cnpj.Should().Be(registration.Condominium.Cnpj.Formatted);
        mine.Condominium.Address.Should().Be(new AddressView("89010000", "Rua XV de Novembro", "1234", "Sala 2", "Centro", "Blumenau", "SC"));
        mine.Manager.FullName.Should().Be("Ana Maria Silva");
        mine.Manager.BirthDate.Should().Be(new DateOnly(1985, 3, 12));
        mine.Manager.Phone.Should().Be("47991234567");
        mine.Manager.Address.City.Should().Be("Blumenau");
    }

    [Fact]
    public async Task Handle_ReturnsTheCpfMasked_AndTheFullNumberAppearsNowhereInTheAnswer()
    {
        ManagerCondominium registration = CondominiumBuilders.Registration();
        Given(registration, displayName: "Ana");

        MyCondominium mine = (await _handler.Handle(new GetMyCondominiumQuery(registration.Manager.UserId), default)).Value;

        mine.Manager.MaskedCpf.Should().Be(registration.Manager.Cpf.Masked);
        mine.Manager.MaskedCpf.Should().StartWith("***.").And.EndWith("-**");
        JsonSerializer.Serialize(mine).Should().NotContain(registration.Manager.Cpf.Value);
    }

    [Fact]
    public async Task Handle_WhenTheManagerHasNoRegistration_FailsWithNotFound()
    {
        Guid userId = Guid.CreateVersion7();
        _profiles.Setup(p => p.FindOfManagerAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync((ManagerCondominium?)null);

        Result<MyCondominium> result = await _handler.Handle(new GetMyCondominiumQuery(userId), default);

        result.Error.Should().Be(CondominiumErrors.NotFound);
        CondominiumErrors.NotFound.Code.Should().Be("condominium.not_found");
        _profile.VerifyNoOtherCalls();
    }

    private void Given(ManagerCondominium registration, string? displayName)
    {
        _profiles.Setup(p => p.FindOfManagerAsync(registration.Manager.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(registration);
        _profile.Setup(p => p.GetDisplayNameAsync(registration.Manager.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(displayName);
    }
}
