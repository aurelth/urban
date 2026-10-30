using Urban.Condominium.Domain.Contacts;
using Urban.Condominium.Domain.Documents;
using Urban.SharedKernel;

namespace Urban.Condominium.Domain.People;

/// <summary>Dados pessoais de quem usa o sistema dentro de um condomínio (hoje, o síndico).</summary>
public sealed class Person
{
    private Person(
        Guid condominiumId,
        Guid userId,
        PersonName fullName,
        Cpf cpf,
        BirthDate birthDate,
        PhoneNumber phone,
        Address address,
        DateTimeOffset createdAt)
    {
        Id = Guid.CreateVersion7();
        CondominiumId = condominiumId;
        UserId = userId;
        FullName = fullName;
        Cpf = cpf;
        BirthDate = birthDate;
        Phone = phone;
        Address = address;
        CreatedAt = createdAt;
    }

    // Usado só pelo EF Core, que não consegue passar o endereço (tipo complexo) pelo construtor ao ler do banco.
    private Person()
    {
        FullName = null!;
        Cpf = null!;
        BirthDate = null!;
        Phone = null!;
        Address = null!;
    }

    public Guid Id { get; }

    public Guid CondominiumId { get; }

    public Guid UserId { get; }

    public PersonName FullName { get; private set; }

    public Cpf Cpf { get; private set; }

    public BirthDate BirthDate { get; private set; }

    public PhoneNumber Phone { get; private set; }

    public Address Address { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public static Result<Person> RegisterManager(
        Guid condominiumId,
        Guid userId,
        PersonName fullName,
        Cpf cpf,
        BirthDate birthDate,
        PhoneNumber phone,
        Address address,
        DateTimeOffset now)
    {
        if (condominiumId == Guid.Empty)
        {
            return Result.Failure<Person>(PersonErrors.CondominiumRequired);
        }

        if (userId == Guid.Empty)
        {
            return Result.Failure<Person>(PersonErrors.UserRequired);
        }

        if (!phone.IsMobile)
        {
            return Result.Failure<Person>(PersonErrors.MobileRequired);
        }

        return Result.Success(new Person(condominiumId, userId, fullName, cpf, birthDate, phone, address, now));
    }

    /// <summary>Dados de contato, de edição livre: celular e endereço.</summary>
    public Result UpdateContact(PhoneNumber phone, Address address)
    {
        if (!phone.IsMobile)
        {
            return Result.Failure(PersonErrors.MobileRequired);
        }

        Phone = phone;
        Address = address;

        return Result.Success();
    }

    /// <summary>Dados de identidade, sensíveis: a confirmação da senha é exigida antes, na camada de Aplicação.</summary>
    public void UpdateIdentity(PersonName fullName, Cpf cpf, BirthDate birthDate)
    {
        FullName = fullName;
        Cpf = cpf;
        BirthDate = birthDate;
    }
}
