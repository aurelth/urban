using Urban.SharedKernel;

namespace Urban.Operations.Domain.Announcements;

public static class AnnouncementCategories
{
    public static Result<AnnouncementCategory> Parse(string? name) =>
        name?.Trim().ToLowerInvariant() switch
        {
            "general" => Result.Success(AnnouncementCategory.General),
            "maintenance" => Result.Success(AnnouncementCategory.Maintenance),
            "important" => Result.Success(AnnouncementCategory.Important),
            _ => Result.Failure<AnnouncementCategory>(AnnouncementErrors.CategoryInvalid),
        };

    /// <summary>O nome que a API usa.</summary>
    public static string NameOf(AnnouncementCategory category) => category switch
    {
        AnnouncementCategory.Maintenance => "maintenance",
        AnnouncementCategory.Important => "important",
        _ => "general",
    };

    public static string NameOf(AnnouncementScope scope) => scope switch
    {
        AnnouncementScope.Block => "block",
        AnnouncementScope.Unit => "unit",
        _ => "condominium",
    };
}
