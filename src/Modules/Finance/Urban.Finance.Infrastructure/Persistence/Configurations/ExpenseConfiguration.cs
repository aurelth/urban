using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Urban.Finance.Domain.Expenses;

namespace Urban.Finance.Infrastructure.Persistence.Configurations;

internal sealed class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public const string RevisionProperty = "Revision";

    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.ToTable("expense");

        builder.HasKey(expense => expense.Id);
        builder.Property(expense => expense.Id).ValueGeneratedNever();

        builder.Property(expense => expense.CondominiumId).IsRequired();
        builder.HasOne<ExpenseGroup>().WithMany().HasForeignKey(expense => expense.GroupId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(expense => expense.Description).HasMaxLength(Expense.TextMaxLength).IsRequired();
        builder.Property(expense => expense.Supplier).HasMaxLength(Expense.TextMaxLength);
        builder.Property(expense => expense.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(expense => expense.StartMonth).HasConversion<YearMonthConverter>().HasMaxLength(YearMonthConverter.Length).IsRequired();
        builder.Property(expense => expense.EndMonth).HasConversion<YearMonthConverter>().HasMaxLength(YearMonthConverter.Length);
        builder.Property(expense => expense.InstallmentCount);
        builder.Property(expense => expense.CurrentInstallment);
        builder.Property(expense => expense.CreatedAt);

        builder.Ignore(expense => expense.LastMonth);
        builder.Ignore(expense => expense.RemainingTotal);

        builder.OwnsMany(
            expense => expense.AmountChanges,
            owned =>
            {
                owned.ToTable("expense_amount_change");
                owned.WithOwner().HasForeignKey("ExpenseId");
                owned.HasKey(change => change.Id);
                owned.Property(change => change.Id).ValueGeneratedNever();
                owned.Property(change => change.FromMonth).HasConversion<YearMonthConverter>().HasMaxLength(YearMonthConverter.Length).IsRequired();
                owned.Property(change => change.Amount).HasPrecision(9, 2);
                owned.HasIndex("ExpenseId", nameof(ExpenseAmountChange.FromMonth)).IsUnique().HasDatabaseName("ix_expense_amount_change_month");
            });

        builder.OwnsMany(
            expense => expense.Installments,
            owned =>
            {
                owned.ToTable("expense_installment");
                owned.WithOwner().HasForeignKey("ExpenseId");
                owned.HasKey(installment => installment.Id);
                owned.Property(installment => installment.Id).ValueGeneratedNever();
                owned.Property(installment => installment.Number);
                owned.Property(installment => installment.Amount).HasPrecision(9, 2);
                owned.HasIndex("ExpenseId", nameof(ExpenseInstallment.Number)).IsUnique().HasDatabaseName("ix_expense_installment_number");
            });

        builder.Navigation(expense => expense.AmountChanges).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(expense => expense.Installments).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(expense => expense.CondominiumId);

        // Mudar só uma parcela ou um reajuste não mexe na linha da despesa, e a versão (xmin) é dela. Esta contagem é incrementada
        // em toda gravação para a linha da despesa mudar junto: assim duas edições da mesma versão nunca passam as duas.
        builder.Property<int>(RevisionProperty).HasDefaultValue(0);

        builder.Property<uint>("xmin").IsRowVersion();
    }
}
