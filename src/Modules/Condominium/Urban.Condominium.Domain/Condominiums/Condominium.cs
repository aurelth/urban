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

    public string Name { get; private set; }

    public Cnpj Cnpj { get; }

    public Address Address { get; private set; }

    public Guid CreatedBy { get; }

    public DateTimeOffset CreatedAt { get; }

    /// <summary>Como as despesas são divididas entre as unidades. Começa na fração ideal; só a escolha fica aqui, o cálculo é do módulo de cobranças.</summary>
    public ApportionmentMethod ApportionmentMethod { get; private set; } = ApportionmentMethod.IdealFraction;

    public static Result<Condominium> Register(string? name, Cnpj cnpj, Address address, Guid createdBy, DateTimeOffset now)
    {
        string normalizedName = TextRules.Normalize(name);

        if (!IsValidName(normalizedName))
        {
            return Result.Failure<Condominium>(CondominiumErrors.NameInvalid);
        }

        if (createdBy == Guid.Empty)
        {
            return Result.Failure<Condominium>(CondominiumErrors.ManagerRequired);
        }

        return Result.Success(new Condominium(normalizedName, cnpj, address, createdBy, now));
    }

    /// <summary>Muda o nome e o endereço. O CNPJ, o identificador e a origem do cadastro não têm caminho de edição.</summary>
    public Result Update(string? name, Address address)
    {
        string normalizedName = TextRules.Normalize(name);

        if (!IsValidName(normalizedName))
        {
            return Result.Failure(CondominiumErrors.NameInvalid);
        }

        Name = normalizedName;
        Address = address;

        return Result.Success();
    }

    public Result ChangeApportionmentMethod(ApportionmentMethod method)
    {
        if (!Enum.IsDefined(method))
        {
            return Result.Failure(CondominiumErrors.ApportionmentMethodInvalid);
        }

        ApportionmentMethod = method;

        return Result.Success();
    }

    /// <summary>Valida e normaliza um nome sem tocar em nenhum condomínio, para quem precisa saber se a edição vai passar antes de gravar qualquer coisa.</summary>
    public static Result<string> NormalizeName(string? name)
    {
        string normalizedName = TextRules.Normalize(name);

        return IsValidName(normalizedName)
            ? Result.Success(normalizedName)
            : Result.Failure<string>(CondominiumErrors.NameInvalid);
    }

    private static bool IsValidName(string normalizedName) =>
        normalizedName.Length is >= NameMinLength and <= NameMaxLength && !TextRules.HasControlCharacters(normalizedName);
}
