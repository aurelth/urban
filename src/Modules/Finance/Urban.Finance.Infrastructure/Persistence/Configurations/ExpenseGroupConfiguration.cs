using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Urban.Finance.Domain.Expenses;

namespace Urban.Finance.Infrastructure.Persistence.Configurations;

internal sealed class ExpenseGroupConfiguration : IEntityTypeConfiguration<ExpenseGroup>
{
    /// <summary>Índice único do nome por condomínio, sem diferenciar maiúsculas. Usa uma expressão, que o EF não modela, então é criado por SQL na migration.</summary>
    public const string NameIndex = "ix_expense_group_name";

    public void Configure(EntityTypeBuilder<ExpenseGroup> builder)
    {
        builder.ToTable("expense_group");

        builder.HasKey(group => group.Id);
        builder.Property(group => group.Id).ValueGeneratedNever();

        builder.Property(group => group.CondominiumId).IsRequired();
        builder.Property(group => group.Name).HasMaxLength(ExpenseGroup.NameMaxLength).IsRequired();
        builder.Property(group => group.CreatedAt);

        builder.HasIndex(group => group.CondominiumId);

        builder.Property<uint>("xmin").IsRowVersion();
    }
}
