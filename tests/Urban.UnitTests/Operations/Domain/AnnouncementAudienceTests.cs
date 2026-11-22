using FluentAssertions;
using Urban.Operations.Domain.Announcements;
using Urban.SharedKernel;

namespace Urban.UnitTests.Operations.Domain;

public class AnnouncementAudienceTests
{
    [Fact]
    public void WholeCondominium_HasNoBlockNorUnit()
    {
        AnnouncementAudience.WholeCondominium.Scope.Should().Be(AnnouncementScope.Condominium);
        AnnouncementAudience.WholeCondominium.Block.Should().BeNull();
        AnnouncementAudience.WholeCondominium.UnitId.Should().BeNull();
    }

    [Fact]
    public void OfBlock_NormalizesTheName()
    {
        Result<AnnouncementAudience> result = AnnouncementAudience.OfBlock("  Bloco   A ");

        result.Value.Scope.Should().Be(AnnouncementScope.Block);
        result.Value.Block.Should().Be("Bloco A");
        result.Value.UnitId.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Bloco\nA")]
    public void OfBlock_AnInvalidName_Fails(string? block)
    {
        AnnouncementAudience.OfBlock(block).Error.Should().Be(AnnouncementErrors.BlockInvalid);
    }

    [Fact]
    public void OfBlock_TheLimit_IsThirtyCharacters()
    {
        AnnouncementAudience.OfBlock(new string('a', 30)).IsSuccess.Should().BeTrue();
        AnnouncementAudience.OfBlock(new string('a', 31)).Error.Should().Be(AnnouncementErrors.BlockInvalid);
    }

    [Fact]
    public void OfUnit_KeepsTheUnit()
    {
        Guid unit = Guid.CreateVersion7();

        Result<AnnouncementAudience> result = AnnouncementAudience.OfUnit(unit);

        result.Value.Scope.Should().Be(AnnouncementScope.Unit);
        result.Value.UnitId.Should().Be(unit);
        result.Value.Block.Should().BeNull();
    }

    [Fact]
    public void OfUnit_WithoutAUnit_Fails()
    {
        AnnouncementAudience.OfUnit(null).Error.Should().Be(AnnouncementErrors.UnitRequired);
        AnnouncementAudience.OfUnit(Guid.Empty).Error.Should().Be(AnnouncementErrors.UnitRequired);
    }

    [Theory]
    [InlineData("condominium")]
    [InlineData("Condominium")]
    [InlineData("  CONDOMINIUM ")]
    public void Parse_TheWholeCondominium_IgnoresTheLeftovers(string scope)
    {
        Result<AnnouncementAudience> result = AnnouncementAudience.Parse(scope, "Bloco A", Guid.CreateVersion7());

        result.Value.Should().Be(AnnouncementAudience.WholeCondominium);
    }

    [Fact]
    public void Parse_ABlock_UsesTheBlockAndIgnoresTheUnit()
    {
        Result<AnnouncementAudience> result = AnnouncementAudience.Parse("block", "Bloco B", Guid.CreateVersion7());

        result.Value.Scope.Should().Be(AnnouncementScope.Block);
        result.Value.Block.Should().Be("Bloco B");
        result.Value.UnitId.Should().BeNull();
    }

    [Fact]
    public void Parse_AUnit_UsesTheUnitAndIgnoresTheBlock()
    {
        Guid unit = Guid.CreateVersion7();

        Result<AnnouncementAudience> result = AnnouncementAudience.Parse("unit", "Bloco B", unit);

        result.Value.Scope.Should().Be(AnnouncementScope.Unit);
        result.Value.UnitId.Should().Be(unit);
        result.Value.Block.Should().BeNull();
    }

    [Fact]
    public void Parse_ABlockScopeWithoutABlock_FailsWithTheBlockError()
    {
        AnnouncementAudience.Parse("block", null, null).Error.Should().Be(AnnouncementErrors.BlockInvalid);
    }

    [Fact]
    public void Parse_AUnitScopeWithoutAUnit_FailsWithTheUnitError()
    {
        AnnouncementAudience.Parse("unit", "Bloco A", null).Error.Should().Be(AnnouncementErrors.UnitRequired);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("everyone")]
    [InlineData("0")]
    public void Parse_AnUnknownScope_Fails(string? scope)
    {
        AnnouncementAudience.Parse(scope, "Bloco A", Guid.CreateVersion7()).Error.Should().Be(AnnouncementErrors.ScopeInvalid);
    }

    [Fact]
    public void Reaches_TheWholeCondominium_ReachesEverybody_EvenWhoLivesNowhere()
    {
        AnnouncementAudience.WholeCondominium.Reaches([], []).Should().BeTrue();
    }

    [Fact]
    public void Reaches_ABlock_ReachesOnlyWhoLivesInIt()
    {
        AnnouncementAudience audience = AnnouncementAudience.OfBlock("Bloco A").Value;

        audience.Reaches(["Bloco A"], []).Should().BeTrue();
        audience.Reaches(["Bloco B", "Bloco A"], []).Should().BeTrue();
        audience.Reaches(["Bloco B"], []).Should().BeFalse();
        audience.Reaches([], [Guid.CreateVersion7()]).Should().BeFalse();
    }

    [Fact]
    public void Reaches_AUnit_ReachesOnlyWhoLivesInIt()
    {
        Guid unit = Guid.CreateVersion7();
        AnnouncementAudience audience = AnnouncementAudience.OfUnit(unit).Value;

        audience.Reaches([], [unit]).Should().BeTrue();
        audience.Reaches([], [Guid.CreateVersion7()]).Should().BeFalse();
        audience.Reaches(["Bloco A"], []).Should().BeFalse();
    }
}
