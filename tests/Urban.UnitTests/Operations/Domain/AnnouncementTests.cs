using FluentAssertions;
using Urban.Operations.Domain.Announcements;
using Urban.SharedKernel;

namespace Urban.UnitTests.Operations.Domain;

public class AnnouncementTests
{
    private static readonly Guid CondominiumId = Guid.CreateVersion7();
    private static readonly Guid Author = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);

    private static Result<Announcement> Create(
        string? title = "Corte de água no sábado",
        string? body = "A água será cortada das 8h às 12h.",
        AnnouncementCategory category = AnnouncementCategory.Maintenance,
        AnnouncementAudience? audience = null,
        Guid? condominiumId = null,
        Guid? author = null) =>
        Announcement.Create(condominiumId ?? CondominiumId, title, body, category, audience ?? AnnouncementAudience.WholeCondominium, author ?? Author, Now);

    [Fact]
    public void Create_StoresEverythingForTheCondominium()
    {
        Result<Announcement> result = Create();

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().NotBe(Guid.Empty);
        result.Value.CondominiumId.Should().Be(CondominiumId);
        result.Value.Title.Should().Be("Corte de água no sábado");
        result.Value.Body.Should().Be("A água será cortada das 8h às 12h.");
        result.Value.Category.Should().Be(AnnouncementCategory.Maintenance);
        result.Value.Scope.Should().Be(AnnouncementScope.Condominium);
        result.Value.Block.Should().BeNull();
        result.Value.UnitId.Should().BeNull();
        result.Value.CreatedBy.Should().Be(Author);
        result.Value.CreatedAt.Should().Be(Now);
        result.Value.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Create_ForABlock_KeepsTheBlockAndNoUnit()
    {
        Announcement announcement = Create(audience: AnnouncementAudience.OfBlock("Bloco A").Value).Value;

        announcement.Scope.Should().Be(AnnouncementScope.Block);
        announcement.Block.Should().Be("Bloco A");
        announcement.UnitId.Should().BeNull();
    }

    [Fact]
    public void Create_ForAUnit_KeepsTheUnitAndNoBlock()
    {
        Guid unit = Guid.CreateVersion7();

        Announcement announcement = Create(audience: AnnouncementAudience.OfUnit(unit).Value).Value;

        announcement.Scope.Should().Be(AnnouncementScope.Unit);
        announcement.UnitId.Should().Be(unit);
        announcement.Block.Should().BeNull();
    }

    [Fact]
    public void Create_NormalizesTheSpacesInTheTitle()
    {
        Create(title: "  Corte   de água ").Value.Title.Should().Be("Corte de água");
    }

    [Fact]
    public void Create_KeepsTheLineBreaksAndTabsOfTheBody_AsLineFeeds_AndTrimsTheEnds()
    {
        Create(body: "  Primeiro parágrafo.\r\n\r\nSegundo\tparágrafo.\rTerceiro.  ").Value.Body.Should().Be("Primeiro parágrafo.\n\nSegundo\tparágrafo.\nTerceiro.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Duas\nlinhas")]
    [InlineData("Com\ttabulação")]
    public void Create_AnInvalidTitle_Fails(string? title)
    {
        Create(title: title).Error.Should().Be(AnnouncementErrors.TitleInvalid);
    }

    [Fact]
    public void Create_TheTitleLimit_IsOneHundredAndTwentyCharacters()
    {
        Create(title: new string('a', 120)).IsSuccess.Should().BeTrue();
        Create(title: new string('a', 121)).Error.Should().Be(AnnouncementErrors.TitleInvalid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \n  ")]
    [InlineData("Com\u0007controle")]
    public void Create_AnInvalidBody_Fails(string? body)
    {
        Create(body: body).Error.Should().Be(AnnouncementErrors.BodyInvalid);
    }

    [Fact]
    public void Create_TheBodyLimit_IsFiveThousandCharacters()
    {
        Create(body: new string('a', 5000)).IsSuccess.Should().BeTrue();
        Create(body: new string('a', 5001)).Error.Should().Be(AnnouncementErrors.BodyInvalid);
    }

    [Fact]
    public void Create_TheTitleIsCheckedBeforeTheBody()
    {
        Create(title: "", body: "").Error.Should().Be(AnnouncementErrors.TitleInvalid);
    }

    [Fact]
    public void Create_WithoutACondominium_Fails()
    {
        Create(condominiumId: Guid.Empty).Error.Should().Be(AnnouncementErrors.CondominiumRequired);
    }

    [Fact]
    public void Create_WithoutAnAuthor_Fails()
    {
        Create(author: Guid.Empty).Error.Should().Be(AnnouncementErrors.AuthorRequired);
    }

    [Fact]
    public void Edit_ReplacesEverythingAndStampsTheMoment()
    {
        Announcement announcement = Create().Value;
        DateTimeOffset later = Now.AddHours(3);
        Guid unit = Guid.CreateVersion7();

        Result result = announcement.Edit("Novo título", "Novo texto", AnnouncementCategory.Important, AnnouncementAudience.OfUnit(unit).Value, later);

        result.IsSuccess.Should().BeTrue();
        announcement.Title.Should().Be("Novo título");
        announcement.Body.Should().Be("Novo texto");
        announcement.Category.Should().Be(AnnouncementCategory.Important);
        announcement.Scope.Should().Be(AnnouncementScope.Unit);
        announcement.UnitId.Should().Be(unit);
        announcement.Block.Should().BeNull();
        announcement.UpdatedAt.Should().Be(later);
        announcement.CreatedAt.Should().Be(Now);
        announcement.CreatedBy.Should().Be(Author);
    }

    [Fact]
    public void Edit_FromABlockToTheWholeCondominium_ClearsTheBlock()
    {
        Announcement announcement = Create(audience: AnnouncementAudience.OfBlock("Bloco A").Value).Value;

        announcement.Edit("Título", "Texto", AnnouncementCategory.General, AnnouncementAudience.WholeCondominium, Now);

        announcement.Scope.Should().Be(AnnouncementScope.Condominium);
        announcement.Block.Should().BeNull();
    }

    [Fact]
    public void Edit_AnInvalidTitleOrBody_FailsAndChangesNothing()
    {
        Announcement announcement = Create().Value;

        announcement.Edit("", "Texto", AnnouncementCategory.General, AnnouncementAudience.WholeCondominium, Now.AddHours(1)).Error.Should().Be(AnnouncementErrors.TitleInvalid);
        announcement.Edit("Título", new string('a', 5001), AnnouncementCategory.General, AnnouncementAudience.WholeCondominium, Now.AddHours(1)).Error.Should().Be(AnnouncementErrors.BodyInvalid);

        announcement.Title.Should().Be("Corte de água no sábado");
        announcement.Category.Should().Be(AnnouncementCategory.Maintenance);
        announcement.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Audience_ReflectsTheScopeBlockAndUnit()
    {
        Guid unit = Guid.CreateVersion7();

        Create(audience: AnnouncementAudience.OfBlock("B").Value).Value.Audience.Should().Be(AnnouncementAudience.OfBlock("B").Value);
        Create(audience: AnnouncementAudience.OfUnit(unit).Value).Value.Audience.Should().Be(AnnouncementAudience.OfUnit(unit).Value);
        Create().Value.Audience.Should().Be(AnnouncementAudience.WholeCondominium);
    }
}
