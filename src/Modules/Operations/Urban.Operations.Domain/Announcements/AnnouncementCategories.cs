using Urban.SharedKernel;

namespace Urban.Operations.Domain.Announcements;

public static class AnnouncementCategories
{
    public static Result<AnnouncementCategory> Parse(string? name) => throw new NotImplementedException();

    /// <summary>O nome que a API usa.</summary>
    public static string NameOf(AnnouncementCategory category) => throw new NotImplementedException();

    public static string NameOf(AnnouncementScope scope) => throw new NotImplementedException();
}
