using Microsoft.EntityFrameworkCore;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Occupancies;
using Urban.Condominium.Domain.People;
using Urban.Condominium.Domain.Units;
using Urban.Condominium.Domain.Vehicles;
using Urban.SharedKernel.Abstractions;
using CondominiumEntity = Urban.Condominium.Domain.Condominiums.Condominium;

namespace Urban.Condominium.Infrastructure.Persistence;

/// <summary>
/// Contexto do módulo Condomínio. Recebe o protetor de dados pessoais (os conversores do CPF o usam) e o tenant da
/// requisição: as tabelas de negócio só devolvem linhas do condomínio dele. A tabela <c>condominium</c> é a raiz do
/// tenant e por isso fica fora do filtro.
/// </summary>
public sealed class CondominiumDbContext : DbContext
{
    public const string Schema = "condominium";

    /// <summary>Coluna sombra com o hash do CPF; preenchida ao gravar, nunca exposta pelo Domain.</summary>
    internal const string CpfHashProperty = "CpfHash";

    private readonly ITenantContext _tenant;

    public CondominiumDbContext(
        DbContextOptions<CondominiumDbContext> options,
        IPersonalDataProtector protector,
        ITenantContext tenant)
        : base(options)
    {
        Protector = protector;
        _tenant = tenant;
    }

    public DbSet<CondominiumEntity> Condominiums => Set<CondominiumEntity>();

    public DbSet<Person> People => Set<Person>();

    public DbSet<CondominiumUnit> Units => Set<CondominiumUnit>();

    public DbSet<Occupancy> Occupancies => Set<Occupancy>();

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    internal IPersonalDataProtector Protector { get; }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampCpfHashes();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampCpfHashes();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.HasCollation(CondominiumPersistence.CollationSchema, CondominiumPersistence.NaturalCollation, locale: "und-u-kn-true", provider: "icu", deterministic: false);
        modelBuilder.ApplyConfiguration(new Configurations.CondominiumConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.PersonConfiguration(Protector));
        modelBuilder.ApplyConfiguration(new Configurations.UnitConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.OccupancyConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.VehicleConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.ChangeLogConfiguration());

        // Sem tenant (SuperAdmin, síndico antes de cadastrar o condomínio, anônimo) a comparação com nulo não casa com
        // nenhuma linha: o padrão é não ver nada. Quem precisa ver tudo pede IgnoreQueryFilters de forma explícita.
        modelBuilder.Entity<Person>().HasQueryFilter(person => person.CondominiumId == _tenant.CondominiumId);
        modelBuilder.Entity<CondominiumUnit>().HasQueryFilter(unit => unit.CondominiumId == _tenant.CondominiumId);
        modelBuilder.Entity<Occupancy>().HasQueryFilter(occupancy => occupancy.CondominiumId == _tenant.CondominiumId);
        modelBuilder.Entity<Vehicle>().HasQueryFilter(vehicle => vehicle.CondominiumId == _tenant.CondominiumId);
    }

    private static bool CpfNeedsAHash(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<Person> entry) =>
        entry.State == EntityState.Added || (entry.State == EntityState.Modified && entry.Property(p => p.Cpf).IsModified);

    /// <summary>O hash nasce junto com a linha e é refeito sempre que o CPF muda, aqui e não nos chamadores: ninguém consegue gravar um CPF sem ele.</summary>
    private void StampCpfHashes()
    {
        ChangeTracker.DetectChanges();
        foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<Person> entry in ChangeTracker.Entries<Person>().Where(CpfNeedsAHash))
        {
            entry.Property<string>(CpfHashProperty).CurrentValue = Protector.Hash(entry.Entity.Cpf.Value);
        }
    }
}
