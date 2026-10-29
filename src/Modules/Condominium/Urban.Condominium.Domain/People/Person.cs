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

    public Guid Id { get; }

    public Guid CondominiumId { get; }

    public Guid UserId { get; }

    public PersonName FullName { get; }

    public Cpf Cpf { get; }

    public BirthDate BirthDate { get; }

    public PhoneNumber Phone { get; }

    public Address Address { get; }

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
}
