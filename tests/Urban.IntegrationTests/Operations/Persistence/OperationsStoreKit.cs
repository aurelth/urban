using FluentAssertions;
using Urban.Operations.Domain.Announcements;
using Urban.Operations.Infrastructure.Persistence;

namespace Urban.IntegrationTests.Operations.Persistence;

/// <summary>Um condomínio de teste e atalhos para guardar comunicados de verdade, cada operação numa "requisição" (contexto) nova.</summary>
internal sealed class OperationsStoreKit(OperationsPostgresFixture postgres)
{
    public static readonly DateTimeOffset Now = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);

    public Guid CondominiumId { get; } = Guid.CreateVersion7();

    public Guid Manager { get; } = Guid.CreateVersion7();

    public OperationsAnnouncements Announcements(Guid? tenant = null) => new(postgres.CreateContext(tenant ?? CondominiumId));

    public OperationsAnnouncements WithoutATenant() => new(postgres.CreateContext());

    public OperationsDbContext Context(Guid? tenant = null) => postgres.CreateContext(tenant);

    public async Task<Announcement> StoreAsync(
        AnnouncementAudience? audience = null,
        string title = "Aviso",
        Guid? author = null,
        DateTimeOffset? at = null,
        Guid? condominiumId = null,
        AnnouncementCategory category = AnnouncementCategory.General)
    {
        Announcement announcement = Announcement.Create(
            condominiumId ?? CondominiumId, title, "Texto do aviso.", category, audience ?? AnnouncementAudience.WholeCondominium, author ?? Manager, at ?? Now).Value;
        (await Announcements(condominiumId).AddAsync(announcement, default)).IsSuccess.Should().BeTrue();

        return announcement;
    }

    public static AnnouncementAudience Block(string name) => AnnouncementAudience.OfBlock(name).Value;

    public static AnnouncementAudience Unit(Guid unitId) => AnnouncementAudience.OfUnit(unitId).Value;
}
