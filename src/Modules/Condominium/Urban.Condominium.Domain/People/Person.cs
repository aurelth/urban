using Urban.Condominium.Domain.Contacts;
using Urban.Condominium.Domain.Documents;
using Urban.SharedKernel;

namespace Urban.Condominium.Domain.People;

/// <summary>
/// Dados pessoais de uma pessoa dentro de um condomínio: o síndico (sempre com conta e endereço próprio) e os moradores
/// (cadastrados pelo síndico, com conta só depois do convite e sem endereço próprio, porque moram na unidade).
/// </summary>
public sealed class Person
{
    private Person(
        Guid condominiumId,
        Guid? userId,
        PersonName fullName,
        Cpf cpf,
        BirthDate birthDate,
        PhoneNumber phone,
        Address? address,
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
    }

    public Guid Id { get; }

    public Guid CondominiumId { get; }

    /// <summary>Nulo enquanto a pessoa não tem acesso ao sistema (morador que ainda não foi convidado).</summary>
    public Guid? UserId { get; }

    public PersonName FullName { get; private set; }

    public Cpf Cpf { get; private set; }

    public BirthDate BirthDate { get; private set; }

    public PhoneNumber Phone { get; private set; }

    /// <summary>Só o síndico tem endereço próprio; o do morador é o da unidade onde ele mora.</summary>
    public Address? Address { get; private set; }

    /// <summary>E-mail de contato, opcional. É para ele que o convite vai; quem não tem e-mail fica cadastrado, sem acesso.</summary>
    public ContactEmail? Email { get; private set; }

    /// <summary>Preenchida nos moradores adultos.</summary>
    public Profession? Profession { get; private set; }

    /// <summary>Preenchido nos moradores adultos.</summary>
    public EmergencyContact? EmergencyContact { get; private set; }

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

    /// <summary>
    /// Cadastra um morador adulto. A pessoa nasce sem conta (<see cref="UserId"/> nulo) e sem endereço próprio. O vínculo com
    /// a unidade é um <c>Occupancy</c>, criado junto.
    /// </summary>
    public static Result<Person> RegisterResident(
        Guid condominiumId,
        PersonName fullName,
        Cpf cpf,
        BirthDate birthDate,
        PhoneNumber phone,
        ContactEmail? email,
        Profession profession,
        EmergencyContact emergencyContact,
        DateTimeOffset now)
    {
        if (condominiumId == Guid.Empty)
        {
            return Result.Failure<Person>(PersonErrors.CondominiumRequired);
        }

        return Result.Success(new Person(condominiumId, null, fullName, cpf, birthDate, phone, null, now)
        {
            Email = email,
            Profession = profession,
            EmergencyContact = emergencyContact,
        });
    }

    /// <summary>
    /// Edição do morador pelo síndico: todos os dados do cadastro de uma vez. A idade já foi conferida ao montar a data de
    /// nascimento, e o CPF repetido é decidido pelo índice único na gravação.
    /// </summary>
    public Result UpdateResident(
        PersonName fullName,
        Cpf cpf,
        BirthDate birthDate,
        PhoneNumber phone,
        ContactEmail? email,
        Profession profession,
        EmergencyContact emergencyContact)
    {
        FullName = fullName;
        Cpf = cpf;
        BirthDate = birthDate;
        Phone = phone;
        Email = email;
        Profession = profession;
        EmergencyContact = emergencyContact;

        return Result.Success();
    }

    /// <summary>O endereço do síndico, que sempre tem um. Chamar para quem não é síndico é erro de programação.</summary>
    public Address RequireAddress() =>
        Address ?? throw new InvalidOperationException("Esta pessoa não tem endereço próprio: só o síndico tem.");

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
