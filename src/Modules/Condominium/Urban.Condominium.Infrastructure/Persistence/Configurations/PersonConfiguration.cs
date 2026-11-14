using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Contacts;
using Urban.Condominium.Domain.Documents;
using Urban.Condominium.Domain.People;
using CondominiumEntity = Urban.Condominium.Domain.Condominiums.Condominium;

namespace Urban.Condominium.Infrastructure.Persistence.Configurations;

internal sealed class PersonConfiguration(IPersonalDataProtector protector) : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.ToTable("person");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        // Referência dentro do módulo: chave estrangeira física. Já a referência ao usuário (outro módulo) é só por ID.
        builder.HasOne<CondominiumEntity>().WithMany().HasForeignKey(p => p.CondominiumId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(p => p.CondominiumId).IsRequired();

        // Nulo enquanto o morador não foi convidado. O índice único aceita vários nulos (cada NULL é distinto no PostgreSQL).
        builder.Property(p => p.UserId);
        builder.HasIndex(p => p.UserId).IsUnique();

        builder.Property(p => p.FullName)
            .HasConversion(name => name.Value, value => PersonName.Create(value).Value)
            .HasMaxLength(120)
            .IsRequired();

        // O CPF só existe cifrado no banco; a unicidade por condomínio usa o hash com chave, em coluna à parte.
        builder.Property(p => p.Cpf)
            .HasColumnName("cpf_encrypted")
            .HasConversion(cpf => protector.Protect(cpf.Value), stored => Cpf.Create(protector.Unprotect(stored)).Value)
            .IsRequired();
        builder.Property<string>(CondominiumDbContext.CpfHashProperty).HasColumnName("cpf_hash").HasMaxLength(64).IsRequired();
        builder.HasIndex(nameof(Person.CondominiumId), CondominiumDbContext.CpfHashProperty).IsUnique();

        builder.Property(p => p.BirthDate)
            .HasConversion(date => date.Value, value => BirthDate.Restore(value))
            .HasColumnType("date")
            .IsRequired();

        builder.Property(p => p.Phone)
            .HasConversion(phone => phone.Value, value => PhoneNumber.Create(value).Value)
            .HasMaxLength(11)
            .IsRequired();

        // Só o síndico tem endereço próprio: no morador as colunas ficam nulas.
        builder.ComplexProperty(p => p.Address, address => address.MapAddress());

        builder.Property(p => p.Email)
            .HasConversion(email => email!.Value, value => ContactEmail.Restore(value))
            .HasMaxLength(ContactEmail.MaxLength);

        builder.Property(p => p.Profession)
            .HasConversion(profession => profession!.Value, value => Profession.Create(value).Value)
            .HasMaxLength(Profession.MaxLength);

        builder.ComplexProperty(p => p.EmergencyContact, contact =>
        {
            contact.Property(c => c.Name)
                .HasConversion(name => name.Value, value => PersonName.Create(value).Value)
                .HasMaxLength(120)
                .IsRequired();
            contact.Property(c => c.Relationship).HasMaxLength(EmergencyContact.RelationshipMaxLength).IsRequired();
            contact.Property(c => c.Phone)
                .HasConversion(phone => phone.Value, value => PhoneNumber.Create(value).Value)
                .HasMaxLength(11)
                .IsRequired();
        });

        builder.Property(p => p.CreatedAt);

        builder.Property<uint>("xmin").IsRowVersion();
    }
}
