namespace Urban.Operations.Domain.Announcements;

/// <summary>A quem o comunicado se dirige. Os nomes da API são <c>condominium</c>, <c>block</c> e <c>unit</c>.</summary>
public enum AnnouncementScope
{
    Condominium = 0,
    Block = 1,
    Unit = 2,
}
