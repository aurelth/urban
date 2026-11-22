namespace Urban.Operations.Domain.Announcements;

/// <summary>O tipo do comunicado, para o morador saber o que esperar antes de abrir. Os nomes da API são <c>general</c>, <c>maintenance</c> e <c>important</c>.</summary>
public enum AnnouncementCategory
{
    General = 0,
    Maintenance = 1,
    Important = 2,
}
