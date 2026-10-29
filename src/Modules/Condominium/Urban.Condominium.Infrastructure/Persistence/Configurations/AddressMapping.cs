using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Urban.Condominium.Domain.Contacts;

namespace Urban.Condominium.Infrastructure.Persistence.Configurations;

/// <summary>O endereço é o mesmo objeto de valor em duas tabelas; o mapeamento fica num lugar só. Os nomes das colunas (<c>address_street</c>...) vêm da convenção snake_case.</summary>
internal static class AddressMapping
{
    public static void MapAddress(this ComplexPropertyBuilder<Address> address)
    {
        address.Property(a => a.Street).HasMaxLength(120).IsRequired();
        address.Property(a => a.Number).HasMaxLength(10).IsRequired();
        address.Property(a => a.Complement).HasMaxLength(60);
        address.Property(a => a.Neighborhood).HasMaxLength(80).IsRequired();
        address.Property(a => a.City).HasMaxLength(80).IsRequired();
        address.Property(a => a.State).HasMaxLength(2).IsRequired();
        address.Property(a => a.PostalCode)
            .HasConversion(code => code.Value, value => PostalCode.Create(value).Value)
            .HasMaxLength(8)
            .IsRequired();
    }
}
