using Microsoft.EntityFrameworkCore;
using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Consumption;
using Urban.Finance.Domain.Expenses;
using Urban.Finance.Domain.Settings;
using Urban.SharedKernel.Abstractions;

namespace Urban.Finance.Infrastructure.Persistence;

/// <summary>
/// Contexto do módulo Financeiro. Recebe o tenant da requisição: as tabelas só devolvem linhas do condomínio dele. Não há chave
/// estrangeira para o módulo Condomínio (os módulos não se enxergam); o <c>condominium_id</c> vem sempre do token.
/// </summary>
public sealed class FinanceDbContext(DbContextOptions<FinanceDbContext> options, ITenantContext tenant) : DbContext(options)
{
    public const string Schema = "finance";

    public DbSet<ExpenseGroup> ExpenseGroups => Set<ExpenseGroup>();

    public DbSet<Expense> Expenses => Set<Expense>();

    public DbSet<ExpenseEntry> ExpenseEntries => Set<ExpenseEntry>();

    public DbSet<MonthClosing> MonthClosings => Set<MonthClosing>();

    public DbSet<UnitCharge> UnitCharges => Set<UnitCharge>();

    public DbSet<UnitChargeLine> UnitChargeLines => Set<UnitChargeLine>();

    public DbSet<FinanceSettings> FinanceSettings => Set<FinanceSettings>();

    public DbSet<ConsumptionTariff> ConsumptionTariffs => Set<ConsumptionTariff>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfiguration(new Configurations.ExpenseGroupConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.ExpenseConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.ExpenseEntryConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.MonthClosingConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.UnitChargeConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.UnitChargeLineConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.FinanceSettingsConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.ConsumptionTariffConfiguration());

        // Sem tenant a comparação com nulo não casa com nenhuma linha: o padrão é não ver nada.
        modelBuilder.Entity<ExpenseGroup>().HasQueryFilter(group => group.CondominiumId == tenant.CondominiumId);
        modelBuilder.Entity<Expense>().HasQueryFilter(expense => expense.CondominiumId == tenant.CondominiumId);
        modelBuilder.Entity<ExpenseEntry>().HasQueryFilter(entry => entry.CondominiumId == tenant.CondominiumId);
        modelBuilder.Entity<MonthClosing>().HasQueryFilter(closing => closing.CondominiumId == tenant.CondominiumId);
        modelBuilder.Entity<UnitCharge>().HasQueryFilter(charge => charge.CondominiumId == tenant.CondominiumId);
        modelBuilder.Entity<UnitChargeLine>().HasQueryFilter(line => line.CondominiumId == tenant.CondominiumId);
        modelBuilder.Entity<FinanceSettings>().HasQueryFilter(settings => settings.CondominiumId == tenant.CondominiumId);
        modelBuilder.Entity<ConsumptionTariff>().HasQueryFilter(tariff => tariff.CondominiumId == tenant.CondominiumId);
    }
}
