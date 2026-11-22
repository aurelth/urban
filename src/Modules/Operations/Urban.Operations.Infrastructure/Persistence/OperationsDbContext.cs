using Microsoft.EntityFrameworkCore;
using Urban.Operations.Domain.Announcements;
using Urban.SharedKernel.Abstractions;

namespace Urban.Operations.Infrastructure.Persistence;

/// <summary>
/// Contexto do módulo Operações. Recebe o tenant da requisição: as tabelas só devolvem linhas do condomínio dele. Não há chave
/// estrangeira para os outros módulos (os módulos não se enxergam); o <c>condominium_id</c> e as contas vêm sempre do token.
/// </summary>
public sealed class OperationsDbContext(DbContextOptions<OperationsDbContext> options, ITenantContext tenant) : DbContext(options)
{
    public const string Schema = "operations";

    public DbSet<Announcement> Announcements => Set<Announcement>();

    public DbSet<AnnouncementRead> AnnouncementReads => Set<AnnouncementRead>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfiguration(new Configurations.AnnouncementConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.AnnouncementReadConfiguration());

        // Sem tenant a comparação com nulo não casa com nenhuma linha: o padrão é não ver nada.
        modelBuilder.Entity<Announcement>().HasQueryFilter(announcement => announcement.CondominiumId == tenant.CondominiumId);
        modelBuilder.Entity<AnnouncementRead>().HasQueryFilter(read => read.CondominiumId == tenant.CondominiumId);
    }
}
