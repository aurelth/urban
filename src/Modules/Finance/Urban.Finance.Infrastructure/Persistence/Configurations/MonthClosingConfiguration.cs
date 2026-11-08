using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Urban.Finance.Domain.Closing;

namespace Urban.Finance.Infrastructure.Persistence.Configurations;

internal sealed class MonthClosingConfiguration : IEntityTypeConfiguration<MonthClosing>
{
    /// <summary>Um fechamento por condomínio e mês: é o que impede fechar o mesmo mês duas vezes ao mesmo tempo.</summary>
    public const string CondominiumMonthIndex = "ix_month_closing_condominium_month";

    public void Configure(EntityTypeBuilder<MonthClosing> builder)
    {
        builder.ToTable("month_closing");

        builder.HasKey(closing => closing.Id);
        builder.Property(closing => closing.Id).ValueGeneratedNever();

        builder.Property(closing => closing.CondominiumId).IsRequired();
        builder.Property(closing => closing.Month).HasConversion<YearMonthConverter>().HasMaxLength(YearMonthConverter.Length).IsRequired();
        builder.Property(closing => closing.DueDate);
        builder.Property(closing => closing.Total).HasPrecision(9, 2);
        builder.Property(closing => closing.BasisKind).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(closing => closing.ClosedBy);
        builder.Property(closing => closing.ClosedAt);

        builder.HasMany(closing => closing.Charges).WithOne().HasForeignKey(charge => charge.ClosingId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(closing => closing.Charges).HasField("_charges").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(closing => new { closing.CondominiumId, closing.Month }).IsUnique().HasDatabaseName(CondominiumMonthIndex);
    }
}
