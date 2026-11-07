using Urban.SharedKernel;

namespace Urban.Finance.Domain.Expenses;

/// <summary>Uma linha do recibo do morador (por exemplo, "Despesas ordinárias" ou "Manutenção das garagens").</summary>
public sealed class ExpenseGroup
{
    public const string DefaultName = "Despesas ordinárias";
    public const int NameMaxLength = 60;

    private ExpenseGroup(Guid condominiumId, string name, DateTimeOffset createdAt)
    {
        Id = Guid.CreateVersion7();
        CondominiumId = condominiumId;
        Name = name;
        CreatedAt = createdAt;
    }

    // Usado só pelo EF Core ao ler do banco.
    private ExpenseGroup()
    {
        Name = null!;
    }

    public Guid Id { get; }

    public Guid CondominiumId { get; }

    public string Name { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public static Result<ExpenseGroup> Create(Guid condominiumId, string? name, DateTimeOffset now)
    {
        if (condominiumId == Guid.Empty)
        {
            return Result.Failure<ExpenseGroup>(ExpenseGroupErrors.CondominiumRequired);
        }

        return TextRules.IsValidText(name, NameMaxLength, out string normalized)
            ? Result.Success(new ExpenseGroup(condominiumId, normalized, now))
            : Result.Failure<ExpenseGroup>(ExpenseGroupErrors.NameInvalid);
    }

    /// <summary>O nome como será guardado (sem espaços repetidos), ou a falha. Serve para conferir se já existe antes de criar ou renomear.</summary>
    public static Result<string> ValidateName(string? name) =>
        TextRules.IsValidText(name, NameMaxLength, out string normalized)
            ? Result.Success(normalized)
            : Result.Failure<string>(ExpenseGroupErrors.NameInvalid);

    public Result Rename(string? name)
    {
        Result<string> valid = ValidateName(name);

        if (valid.IsFailure)
        {
            return Result.Failure(valid.Error);
        }

        Name = valid.Value;
        return Result.Success();
    }
}
