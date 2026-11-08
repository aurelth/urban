using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Urban.Finance.Domain.Expenses;

namespace Urban.Finance.Infrastructure.Persistence.Configurations;

internal sealed class ExpenseEntryConfiguration : IEntityTypeConfiguration<ExpenseEntry>
{
    /// <summary>Um lançamento por despesa e mês: é o que torna a abertura do mês repetível sem duplicar.</summary>
    public const string ExpenseMonthIndex = "ix_expense_entry_expense_month";

    public void Configure(EntityTypeBuilder<ExpenseEntry> builder)
    {
        builder.ToTable("expense_entry");

        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Id).ValueGeneratedNever();

        builder.Property(entry => entry.CondominiumId).IsRequired();
        builder.HasOne<Expense>().WithMany().HasForeignKey(entry => entry.ExpenseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ExpenseGroup>().WithMany().HasForeignKey(entry => entry.GroupId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(entry => entry.Month).HasConversion<YearMonthConverter>().HasMaxLength(YearMonthConverter.Length).IsRequired();
        builder.Property(entry => entry.Description).HasMaxLength(Expense.TextMaxLength).IsRequired();
        builder.Property(entry => entry.Amount).HasPrecision(9, 2);
        builder.Property(entry => entry.Label).HasMaxLength(10);
        builder.Property(entry => entry.DueDate);

        // Só Pending, Paid e Canceled são gravados: "vencido" é calculado pela data de vencimento a cada leitura.
        builder.Property(entry => entry.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(entry => entry.CreatedAt);

        builder.Ignore(entry => entry.NeedsAmount);

        builder.HasIndex(entry => new { entry.ExpenseId, entry.Month }).IsUnique().HasDatabaseName(ExpenseMonthIndex);
        builder.HasIndex(entry => new { entry.CondominiumId, entry.Month });

        builder.Property<uint>("xmin").IsRowVersion();
    }
}
