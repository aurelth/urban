using FluentAssertions;
using Moq;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Application.Residents;
using Urban.Condominium.Domain.People;
using Urban.SharedKernel.Contracts;

namespace Urban.UnitTests.Condominium.Application.Residents;

/// <summary>
/// A situação do acesso vem do Identity, que é dono das contas. A lista pergunta uma vez por página (nada de uma consulta por
/// morador) e só pelas pessoas que já têm conta.
/// </summary>
public class ResidentViewsTests
{
    private readonly ResidentsTestKit _kit = new();

    private ResidentRecord Record(string name, ResidentAccessStatus? status = null)
    {
        Person person = _kit.NewResident(name, $"{name.Replace(' ', '.').ToLowerInvariant()}@exemplo.com");
        if (status is { } given)
        {
            _kit.WithAccount(person, given);
        }

        return new ResidentRecord(person, [], "1");
    }

    [Fact]
    public async Task View_OfAResidentWithoutAnAccount_IsNoneAndNeverAsksTheIdentityModule()
    {
        ResidentView view = await _kit.Views.ViewAsync(Record("Ana Lima"), default);

        view.AccessStatus.Should().Be(ResidentAccessStatus.None);
        _kit.Access.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(ResidentAccessStatus.Invited)]
    [InlineData(ResidentAccessStatus.Active)]
    [InlineData(ResidentAccessStatus.Inactive)]
    public async Task View_OfAResidentWithAnAccount_CarriesTheStatusTheIdentityModuleReports(ResidentAccessStatus status)
    {
        ResidentView view = await _kit.Views.ViewAsync(Record("Ana Lima", status), default);

        view.AccessStatus.Should().Be(status);
    }

    [Fact]
    public async Task View_WhenTheIdentityModuleDoesNotKnowTheAccount_IsInactive_NeverAGuessOfActive()
    {
        ResidentRecord record = Record("Ana Lima");
        record.Person.AttachUser(Guid.CreateVersion7());

        ResidentView view = await _kit.Views.ViewAsync(record, default);

        view.AccessStatus.Should().Be(ResidentAccessStatus.Inactive);
    }

    [Fact]
    public async Task ViewAll_AsksTheIdentityModuleOnceForThePage_AndOnlyAboutTheResidentsWhoHaveAnAccount()
    {
        ResidentRecord invited = Record("Ana Lima", ResidentAccessStatus.Invited);
        ResidentRecord active = Record("Beto Rocha", ResidentAccessStatus.Active);
        ResidentRecord none = Record("Caio Dias");
        IReadOnlyCollection<Guid>? asked = null;
        _kit.Access
            .Setup(a => a.GetStatusesAsync(_kit.CondominiumId, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, IReadOnlyCollection<Guid>, CancellationToken>((_, ids, _) => asked = ids)
            .ReturnsAsync(new Dictionary<Guid, ResidentAccessStatus>
            {
                [invited.Person.UserId!.Value] = ResidentAccessStatus.Invited,
                [active.Person.UserId!.Value] = ResidentAccessStatus.Active,
            });

        IReadOnlyList<ResidentView> views = await _kit.Views.ViewAllAsync([invited, active, none], default);

        views.Select(v => v.AccessStatus).Should().Equal(ResidentAccessStatus.Invited, ResidentAccessStatus.Active, ResidentAccessStatus.None);
        _kit.Access.Verify(a => a.GetStatusesAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()), Times.Once);
        asked.Should().BeEquivalentTo([invited.Person.UserId!.Value, active.Person.UserId!.Value]);
    }

    [Fact]
    public async Task ViewAll_WithNobodyWhoHasAnAccount_DoesNotAskAtAll()
    {
        IReadOnlyList<ResidentView> views = await _kit.Views.ViewAllAsync([Record("Ana Lima"), Record("Beto Rocha")], default);

        views.Should().HaveCount(2);
        _kit.Access.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ViewAll_OfAnEmptyPage_ReturnsAnEmptyList()
    {
        (await _kit.Views.ViewAllAsync([], default)).Should().BeEmpty();
        _kit.Access.VerifyNoOtherCalls();
    }
}
