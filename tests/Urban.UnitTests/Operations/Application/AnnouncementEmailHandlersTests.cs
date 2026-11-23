using FluentAssertions;
using Urban.Operations.Application.Abstractions;
using Urban.Operations.Application.Announcements;
using Urban.Operations.Domain;
using Urban.Operations.Domain.Announcements;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;
using Urban.UnitTests.Finance.Application;

namespace Urban.UnitTests.Operations.Application;

public class AnnouncementEmailHandlersTests
{
    private static readonly Uri Mural = new("https://urban-pro.io/mural");

    private readonly AnnouncementTestKit _kit = new();
    private readonly FakeAccountEmails _accounts = new();
    private readonly FakeEmailSender _email = new();

    private FanOutAnnouncementEmailsCommandHandler FanOutHandler() => new(_kit.Announcements, _kit.Directory, _kit.Mail, _kit.Tenant, _kit.Clock);

    private SendAnnouncementEmailCommandHandler SendHandler() => new(_kit.Announcements, _accounts, new FixedLinks(Mural), _email, _kit.Tenant);

    // ---- Descobrir quem recebe ----

    [Fact]
    public async Task FanOut_OpensOneDeliveryForEachAccountThatLivesInTheScope()
    {
        ResidentUnit a101 = _kit.NewUnit("Bloco A", "101");
        ResidentUnit b201 = _kit.NewUnit("Bloco B", "201");
        Guid ana = _kit.NewResident(a101);
        Guid bia = _kit.NewResident(b201);
        Announcement announcement = _kit.NewAnnouncementWithEmail();

        Result<int> result = await FanOutHandler().Handle(new FanOutAnnouncementEmailsCommand(announcement.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(2);
        _kit.Mail.Deliveries.Should().BeEquivalentTo(
            [(_kit.CondominiumId, announcement.Id, ana), (_kit.CondominiumId, announcement.Id, bia)]);
    }

    [Fact]
    public async Task FanOut_ForABlock_OnlyReachesWhoLivesInThatBlock()
    {
        ResidentUnit a101 = _kit.NewUnit("Bloco A", "101");
        ResidentUnit b201 = _kit.NewUnit("Bloco B", "201");
        Guid ana = _kit.NewResident(a101);
        _kit.NewResident(b201);
        Announcement announcement = _kit.NewAnnouncementWithEmail(AnnouncementTestKit.Block("Bloco A"));

        Result<int> result = await FanOutHandler().Handle(new FanOutAnnouncementEmailsCommand(announcement.Id), default);

        result.Value.Should().Be(1);
        _kit.Mail.Deliveries.Should().ContainSingle().Which.UserId.Should().Be(ana);
    }

    [Fact]
    public async Task FanOut_ForAUnit_ReachesEveryoneWhoLivesInIt()
    {
        ResidentUnit a101 = _kit.NewUnit("Bloco A", "101");
        ResidentUnit a102 = _kit.NewUnit("Bloco A", "102");
        Guid owner = _kit.NewResident(a101);
        Guid tenant = _kit.NewResident(a101);
        _kit.NewResident(a102);
        Announcement announcement = _kit.NewAnnouncementWithEmail(AnnouncementTestKit.Unit(a101));

        await FanOutHandler().Handle(new FanOutAnnouncementEmailsCommand(announcement.Id), default);

        _kit.Mail.Deliveries.Select(delivery => delivery.UserId).Should().BeEquivalentTo([owner, tenant]);
    }

    [Fact]
    public async Task FanOut_NeverWritesToTheManagerWhoWroteTheAnnouncement()
    {
        ResidentUnit a101 = _kit.NewUnit("Bloco A", "101");
        _kit.Directory.Live(_kit.Manager, a101);
        Guid ana = _kit.NewResident(a101);
        Announcement announcement = _kit.NewAnnouncementWithEmail();

        Result<int> result = await FanOutHandler().Handle(new FanOutAnnouncementEmailsCommand(announcement.Id), default);

        result.Value.Should().Be(1);
        _kit.Mail.Deliveries.Should().ContainSingle().Which.UserId.Should().Be(ana);
    }

    [Fact]
    public async Task FanOut_AccountsLivingInSeveralUnitsOfTheScope_GetOneDelivery()
    {
        ResidentUnit a101 = _kit.NewUnit("Bloco A", "101");
        ResidentUnit a102 = _kit.NewUnit("Bloco A", "102");
        Guid owner = _kit.NewResident(a101);
        _kit.Directory.Live(owner, a102);
        Announcement announcement = _kit.NewAnnouncementWithEmail();

        Result<int> result = await FanOutHandler().Handle(new FanOutAnnouncementEmailsCommand(announcement.Id), default);

        result.Value.Should().Be(1);
    }

    [Fact]
    public async Task FanOut_AsksTheCondominiumOnceAndUsesTodayInBrasilia()
    {
        _kit.Clock.Advance(TimeSpan.FromHours(13));
        ResidentUnit a101 = _kit.NewUnit("Bloco A", "101");
        _kit.NewResident(a101);
        _kit.NewResident(a101);
        Announcement announcement = _kit.NewAnnouncementWithEmail();

        await FanOutHandler().Handle(new FanOutAnnouncementEmailsCommand(announcement.Id), default);

        _kit.Directory.RecipientsCalls.Should().Be(1);
        // 01h UTC de 23/11 ainda é noite de 22/11 em Brasília.
        _kit.Directory.LastToday.Should().Be(new DateOnly(2026, 11, 22));
    }

    [Fact]
    public async Task FanOut_WithNobodyLivingThere_OpensNothing()
    {
        _kit.NewUnit("Bloco A", "101");
        Announcement announcement = _kit.NewAnnouncementWithEmail();

        Result<int> result = await FanOutHandler().Handle(new FanOutAnnouncementEmailsCommand(announcement.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0);
        _kit.Mail.Deliveries.Should().BeEmpty();
    }

    [Fact]
    public async Task FanOut_ForAnAnnouncementThatDidNotAskForEmail_OpensNothing()
    {
        ResidentUnit a101 = _kit.NewUnit("Bloco A", "101");
        _kit.NewResident(a101);
        Announcement announcement = _kit.NewAnnouncement();

        Result<int> result = await FanOutHandler().Handle(new FanOutAnnouncementEmailsCommand(announcement.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0);
        _kit.Mail.Deliveries.Should().BeEmpty();
        _kit.Directory.RecipientsCalls.Should().Be(0);
    }

    [Fact]
    public async Task FanOut_ForAnAnnouncementThatWasDeletedInTheMeantime_DoesNothing()
    {
        Result<int> result = await FanOutHandler().Handle(new FanOutAnnouncementEmailsCommand(Guid.CreateVersion7()), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0);
        _kit.Mail.Deliveries.Should().BeEmpty();
    }

    [Fact]
    public async Task FanOut_WithoutACondominium_Fails()
    {
        Announcement announcement = _kit.NewAnnouncementWithEmail();

        Result<int> result = await new FanOutAnnouncementEmailsCommandHandler(_kit.Announcements, _kit.Directory, _kit.Mail, _kit.WithoutATenant().Tenant, _kit.Clock)
            .Handle(new FanOutAnnouncementEmailsCommand(announcement.Id), default);

        result.Error.Should().Be(OperationsErrors.CondominiumNotFound);
    }

    // ---- Enviar a uma conta ----

    [Fact]
    public async Task Send_WritesToTheAccountEmail_WithTheAnnouncementAndTheMuralLink()
    {
        Guid ana = Guid.CreateVersion7();
        _accounts.Add(ana, "ana@example.com");
        _accounts.Add(_kit.Manager, "sindico@example.com");
        Announcement announcement = _kit.NewAnnouncementWithEmail(title: "Água cortada na terça", category: AnnouncementCategory.Important, body: "A rua será interditada.\nVolta às 18h.");

        Result result = await SendHandler().Handle(new SendAnnouncementEmailCommand(announcement.Id, ana), default);

        result.IsSuccess.Should().BeTrue();
        EmailMessage message = _email.Sent.Should().ContainSingle().Subject;
        message.To.Should().Be("ana@example.com");
        message.Subject.Should().Be("Importante: Água cortada na terça");
        string[] lines = message.TextBody.Split('\n');
        lines.Should().Contain("O síndico publicou um comunicado para todo o condomínio no Urban.");
        lines.Should().Contain("Importante: Água cortada na terça");
        lines.Should().ContainInOrder("A rua será interditada.", "Volta às 18h.");
        lines.Should().Contain("https://urban-pro.io/mural");
        lines.Should().Contain("Para responder, é só responder a este e-mail: a resposta chega ao síndico.");
        message.ReplyTo.Should().Be("sindico@example.com");
        message.Attachments.Should().BeNull();
    }

    [Theory]
    [InlineData(AnnouncementCategory.General, "Aviso")]
    [InlineData(AnnouncementCategory.Maintenance, "Manutenção: Aviso")]
    [InlineData(AnnouncementCategory.Important, "Importante: Aviso")]
    public async Task Send_TheSubjectNamesTheCategoryUnlessItIsGeneral(AnnouncementCategory category, string subject)
    {
        Guid ana = Guid.CreateVersion7();
        _accounts.Add(ana, "ana@example.com");
        Announcement announcement = _kit.NewAnnouncementWithEmail(title: "Aviso", category: category);

        await SendHandler().Handle(new SendAnnouncementEmailCommand(announcement.Id, ana), default);

        _email.Sent.Single().Subject.Should().Be(subject);
    }

    [Fact]
    public async Task Send_TheTextSaysWhoTheAnnouncementIsFor()
    {
        Guid ana = Guid.CreateVersion7();
        _accounts.Add(ana, "ana@example.com");
        ResidentUnit a101 = _kit.NewUnit("Bloco A", "101");
        Announcement forBlock = _kit.NewAnnouncementWithEmail(AnnouncementTestKit.Block("Bloco A"));
        Announcement forUnit = _kit.NewAnnouncementWithEmail(AnnouncementTestKit.Unit(a101));

        await SendHandler().Handle(new SendAnnouncementEmailCommand(forBlock.Id, ana), default);
        await SendHandler().Handle(new SendAnnouncementEmailCommand(forUnit.Id, ana), default);

        _email.Sent[0].TextBody.Split('\n').Should().Contain("O síndico publicou um comunicado para o bloco Bloco A no Urban.");
        _email.Sent[1].TextBody.Split('\n').Should().Contain("O síndico publicou um comunicado para a sua unidade no Urban.");
    }

    [Fact]
    public async Task Send_AsksTheAccountDirectoryForTheCondominiumOfTheRequest()
    {
        Guid ana = Guid.CreateVersion7();
        _accounts.Add(ana, "ana@example.com");
        Announcement announcement = _kit.NewAnnouncementWithEmail();

        await SendHandler().Handle(new SendAnnouncementEmailCommand(announcement.Id, ana), default);

        _accounts.Lookups.Should().Equal((ana, _kit.CondominiumId), (_kit.Manager, _kit.CondominiumId));
    }

    [Fact]
    public async Task Send_TheReplyGoesToTheManagerWhoPublished_NotToTheSystemSender()
    {
        Guid ana = Guid.CreateVersion7();
        Guid anotherManager = Guid.CreateVersion7();
        _accounts.Add(ana, "ana@example.com");
        _accounts.Add(_kit.Manager, "sindico@example.com");
        _accounts.Add(anotherManager, "outro@example.com");
        Announcement announcement = _kit.NewAnnouncementWithEmail();

        await SendHandler().Handle(new SendAnnouncementEmailCommand(announcement.Id, ana), default);

        _email.Sent.Single().ReplyTo.Should().Be("sindico@example.com");
    }

    [Fact]
    public async Task Send_WhenTheManagerAccountCannotReceiveAnyMore_StillSendsButWithoutAReplyAddress()
    {
        Guid ana = Guid.CreateVersion7();
        _accounts.Add(ana, "ana@example.com");
        Announcement announcement = _kit.NewAnnouncementWithEmail();

        Result result = await SendHandler().Handle(new SendAnnouncementEmailCommand(announcement.Id, ana), default);

        result.IsSuccess.Should().BeTrue();
        EmailMessage message = _email.Sent.Should().ContainSingle().Subject;
        message.ReplyTo.Should().BeNull();
        message.TextBody.Should().NotContain("é só responder a este e-mail");
    }

    [Fact]
    public async Task Send_ToAnAccountThatCannotReceiveAnyMore_SendsNothingAndIsNotAnError()
    {
        Announcement announcement = _kit.NewAnnouncementWithEmail();

        Result result = await SendHandler().Handle(new SendAnnouncementEmailCommand(announcement.Id, Guid.CreateVersion7()), default);

        result.IsSuccess.Should().BeTrue();
        _email.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Send_OfAnAnnouncementDeletedInTheMeantime_SendsNothingAndIsNotAnError()
    {
        Guid ana = Guid.CreateVersion7();
        _accounts.Add(ana, "ana@example.com");

        Result result = await SendHandler().Handle(new SendAnnouncementEmailCommand(Guid.CreateVersion7(), ana), default);

        result.IsSuccess.Should().BeTrue();
        _email.Sent.Should().BeEmpty();
        _accounts.Lookups.Should().BeEmpty();
    }

    [Fact]
    public async Task Send_OfAnAnnouncementThatDidNotAskForEmail_SendsNothing()
    {
        Guid ana = Guid.CreateVersion7();
        _accounts.Add(ana, "ana@example.com");
        Announcement announcement = _kit.NewAnnouncement();

        Result result = await SendHandler().Handle(new SendAnnouncementEmailCommand(announcement.Id, ana), default);

        result.IsSuccess.Should().BeTrue();
        _email.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Send_WhenTheProviderRefuses_LetsTheFailureRiseSoTheQueueTriesAgain()
    {
        Guid ana = Guid.CreateVersion7();
        _accounts.Add(ana, "ana@example.com");
        Announcement announcement = _kit.NewAnnouncementWithEmail();
        _email.Refuse = true;

        Func<Task> act = () => SendHandler().Handle(new SendAnnouncementEmailCommand(announcement.Id, ana), default);

        await act.Should().ThrowAsync<EmailDeliveryException>();
    }

    [Fact]
    public async Task Send_WithoutACondominium_FailsAndSendsNothing()
    {
        Guid ana = Guid.CreateVersion7();
        _accounts.Add(ana, "ana@example.com");
        Announcement announcement = _kit.NewAnnouncementWithEmail();

        Result result = await new SendAnnouncementEmailCommandHandler(_kit.Announcements, _accounts, new FixedLinks(Mural), _email, _kit.WithoutATenant().Tenant)
            .Handle(new SendAnnouncementEmailCommand(announcement.Id, ana), default);

        result.Error.Should().Be(OperationsErrors.CondominiumNotFound);
        _email.Sent.Should().BeEmpty();
    }
}

/// <summary>As contas ativas de mentira: só quem foi cadastrado tem e-mail; as consultas ficam registradas.</summary>
internal sealed class FakeAccountEmails : IAccountEmails
{
    private readonly Dictionary<Guid, string> _emails = [];

    public List<(Guid UserId, Guid CondominiumId)> Lookups { get; } = [];

    public void Add(Guid userId, string email) => _emails[userId] = email;

    public Task<string?> FindActiveEmailAsync(Guid userId, Guid condominiumId, CancellationToken cancellationToken)
    {
        Lookups.Add((userId, condominiumId));

        return Task.FromResult(_emails.GetValueOrDefault(userId));
    }
}

internal sealed class FixedLinks(Uri mural) : IAnnouncementLinks
{
    public Uri GetMuralUri() => mural;
}
