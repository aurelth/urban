using FluentAssertions;
using Urban.Operations.Domain.Announcements;

namespace Urban.UnitTests.Operations.Domain;

public class AnnouncementCategoriesTests
{
    [Theory]
    [InlineData("general", AnnouncementCategory.General)]
    [InlineData("maintenance", AnnouncementCategory.Maintenance)]
    [InlineData("important", AnnouncementCategory.Important)]
    [InlineData("  Important ", AnnouncementCategory.Important)]
    [InlineData("MAINTENANCE", AnnouncementCategory.Maintenance)]
    public void Parse_KnownNames_IgnoreCaseAndSpaces(string name, AnnouncementCategory expected)
    {
        AnnouncementCategories.Parse(name).Value.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("urgent")]
    [InlineData("0")]
    public void Parse_AnUnknownName_Fails(string? name)
    {
        AnnouncementCategories.Parse(name).Error.Should().Be(AnnouncementErrors.CategoryInvalid);
    }

    [Theory]
    [InlineData(AnnouncementCategory.General, "general")]
    [InlineData(AnnouncementCategory.Maintenance, "maintenance")]
    [InlineData(AnnouncementCategory.Important, "important")]
    public void NameOf_ACategory_IsTheApiName(AnnouncementCategory category, string expected)
    {
        AnnouncementCategories.NameOf(category).Should().Be(expected);
    }

    [Theory]
    [InlineData(AnnouncementScope.Condominium, "condominium")]
    [InlineData(AnnouncementScope.Block, "block")]
    [InlineData(AnnouncementScope.Unit, "unit")]
    public void NameOf_AScope_IsTheApiName(AnnouncementScope scope, string expected)
    {
        AnnouncementCategories.NameOf(scope).Should().Be(expected);
    }
}
