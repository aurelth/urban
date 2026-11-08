using Microsoft.EntityFrameworkCore;
using Urban.Finance.Domain.Expenses;
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfiguration(new Configurations.ExpenseGroupConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.ExpenseConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.ExpenseEntryConfiguration());

        // Sem tenant a comparação com nulo não casa com nenhuma linha: o padrão é não ver nada.
        modelBuilder.Entity<ExpenseGroup>().HasQueryFilter(group => group.CondominiumId == tenant.CondominiumId);
        modelBuilder.Entity<Expense>().HasQueryFilter(expense => expense.CondominiumId == tenant.CondominiumId);
        modelBuilder.Entity<ExpenseEntry>().HasQueryFilter(entry => entry.CondominiumId == tenant.CondominiumId);
    }
}
