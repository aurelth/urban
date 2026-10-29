using Urban.Condominium.Domain.Contacts;
using Urban.Condominium.Domain.Documents;
using Urban.SharedKernel;

namespace Urban.Condominium.Domain.Condominiums;

/// <summary>Raiz de agregado do condomínio. É a fronteira de dados entre clientes (o tenant).</summary>
public sealed class Condominium
{
    private const int NameMinLength = 2;
    private const int NameMaxLength = 120;

    private Condominium(string name, Cnpj cnpj, Address address, Guid createdBy, DateTimeOffset createdAt)
    {
        Id = Guid.CreateVersion7();
        Name = name;
        Cnpj = cnpj;
        Address = address;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
    }

    // Usado só pelo EF Core, que não consegue passar o endereço (tipo complexo) pelo construtor ao ler do banco.
    private Condominium()
    {
        Name = null!;
        Cnpj = null!;
        Address = null!;
    }

    public Guid Id { get; }

    public string Name { get; }

    public Cnpj Cnpj { get; }

    public Address Address { get; }

    public Guid CreatedBy { get; }

    public DateTimeOffset CreatedAt { get; }

    public static Result<Condominium> Register(string? name, Cnpj cnpj, Address address, Guid createdBy, DateTimeOffset now)
    {
        string normalizedName = TextRules.Normalize(name);

        if (normalizedName.Length is < NameMinLength or > NameMaxLength || TextRules.HasControlCharacters(normalizedName))
        {
            return Result.Failure<Condominium>(CondominiumErrors.NameInvalid);
        }

        if (createdBy == Guid.Empty)
        {
            return Result.Failure<Condominium>(CondominiumErrors.ManagerRequired);
        }

        return Result.Success(new Condominium(normalizedName, cnpj, address, createdBy, now));
    }
}
