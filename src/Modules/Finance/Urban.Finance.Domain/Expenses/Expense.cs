using System.Globalization;
using Urban.Finance.Domain.Months;
using Urban.SharedKernel;

namespace Urban.Finance.Domain.Expenses;

/// <summary>
/// A regra de uma despesa do condomínio. Tem sempre uma de quatro formas: única (um mês), fixa mensal (mesmo valor todo mês,
/// com histórico de reajustes), variável mensal (valor digitado a cada mês) ou parcelada. Os lançamentos mensais são gerados a partir dela.
/// </summary>
public sealed class Expense
{
    public const int TextMaxLength = 120;

    private readonly List<ExpenseAmountChange> _amountChanges = [];
    private readonly List<ExpenseInstallment> _installments = [];

    private Expense(
        Guid condominiumId,
        Guid groupId,
        ExpenseText text,
        ExpenseKind kind,
        YearMonth startMonth,
        int dueDay,
        DateTimeOffset createdAt)
    {
        Id = Guid.CreateVersion7();
        CondominiumId = condominiumId;
        GroupId = groupId;
        Description = text.Description;
        Supplier = text.Supplier;
        Kind = kind;
        StartMonth = startMonth;
        DueDay = dueDay;
        CreatedAt = createdAt;
    }

    // Usado só pelo EF Core ao ler do banco.
    private Expense()
    {
        Description = null!;
    }

    public Guid Id { get; }

    public Guid CondominiumId { get; }

    public Guid GroupId { get; private set; }

    public string Description { get; private set; }

    public string? Supplier { get; private set; }

    public ExpenseKind Kind { get; }

    public YearMonth StartMonth { get; private set; }

    /// <summary>Dia do mês em que a despesa vence (1 a 31). Nos meses mais curtos vale o último dia do mês.</summary>
    public int DueDay { get; private set; }

    public YearMonth? EndMonth { get; private set; }

    public int? InstallmentCount { get; private set; }

    public int? CurrentInstallment { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public IReadOnlyList<ExpenseAmountChange> AmountChanges => _amountChanges;

    public IReadOnlyList<ExpenseInstallment> Installments => _installments;

    /// <summary>Último mês com lançamento. Nulo quando a despesa não tem fim (fixa e variável sem mês final).</summary>
    public YearMonth? LastMonth => Kind switch
    {
        ExpenseKind.Single => StartMonth,
        ExpenseKind.Installment => StartMonth.AddMonths(InstallmentCount!.Value - CurrentInstallment!.Value),
        _ => EndMonth,
    };

    /// <summary>Soma das parcelas que ainda faltam, a partir da parcela atual.</summary>
    public decimal RemainingTotal => _installments
        .Where(installment => installment.Number >= CurrentInstallment)
        .Sum(installment => installment.Amount);

    public static Result<Expense> CreateSingle(
        Guid condominiumId, Guid groupId, string? description, string? supplier, YearMonth month, decimal amount, int dueDay, DateTimeOffset now)
    {
        Result<Expense> created = CreateBase(condominiumId, groupId, description, supplier, ExpenseKind.Single, month, dueDay, now);

        if (created.IsFailure)
        {
            return created;
        }

        if (!MoneyRules.IsValid(amount))
        {
            return Result.Failure<Expense>(ExpenseErrors.AmountInvalid);
        }

        created.Value._amountChanges.Add(new ExpenseAmountChange(month, amount));
        return created;
    }

    public static Result<Expense> CreateFixed(
        Guid condominiumId,
        Guid groupId,
        string? description,
        string? supplier,
        YearMonth startMonth,
        YearMonth? endMonth,
        decimal amount,
        int dueDay,
        DateTimeOffset now)
    {
        Result<Expense> created = CreateBase(condominiumId, groupId, description, supplier, ExpenseKind.Fixed, startMonth, dueDay, now);

        if (created.IsFailure)
        {
            return created;
        }

        if (!MoneyRules.IsValid(amount))
        {
            return Result.Failure<Expense>(ExpenseErrors.AmountInvalid);
        }

        if (endMonth < startMonth)
        {
            return Result.Failure<Expense>(ExpenseErrors.EndBeforeStart);
        }

        created.Value.EndMonth = endMonth;
        created.Value._amountChanges.Add(new ExpenseAmountChange(startMonth, amount));
        return created;
    }

    public static Result<Expense> CreateVariable(
        Guid condominiumId, Guid groupId, string? description, string? supplier, YearMonth startMonth, YearMonth? endMonth, int dueDay, DateTimeOffset now)
    {
        Result<Expense> created = CreateBase(condominiumId, groupId, description, supplier, ExpenseKind.Variable, startMonth, dueDay, now);

        if (created.IsFailure)
        {
            return created;
        }

        if (endMonth < startMonth)
        {
            return Result.Failure<Expense>(ExpenseErrors.EndBeforeStart);
        }

        created.Value.EndMonth = endMonth;
        return created;
    }

    /// <summary>
    /// Despesa parcelada. <paramref name="currentInstallment"/> é a parcela que cai em <paramref name="firstMonth"/>:
    /// uma compra que já está na 4/6 começa com 4 e termina na 6.
    /// </summary>
    public static Result<Expense> CreateInstallment(
        Guid condominiumId,
        Guid groupId,
        string? description,
        string? supplier,
        decimal total,
        int installmentCount,
        YearMonth firstMonth,
        int currentInstallment,
        int dueDay,
        DateTimeOffset now)
    {
        Result<Expense> created = CreateBase(condominiumId, groupId, description, supplier, ExpenseKind.Installment, firstMonth, dueDay, now);

        if (created.IsFailure)
        {
            return created;
        }

        Result<IReadOnlyList<decimal>> amounts = InstallmentPlan.Split(total, installmentCount);

        if (amounts.IsFailure)
        {
            return Result.Failure<Expense>(amounts.Error);
        }

        if (currentInstallment < 1 || currentInstallment > installmentCount)
        {
            return Result.Failure<Expense>(ExpenseErrors.CurrentInstallmentInvalid);
        }

        Expense expense = created.Value;
        expense.InstallmentCount = installmentCount;
        expense.CurrentInstallment = currentInstallment;
        expense._installments.AddRange(amounts.Value.Select((amount, index) => new ExpenseInstallment(index + 1, amount)));
        return created;
    }

    public Result Edit(string? description, string? supplier, Guid groupId, int dueDay)
    {
        Result<ExpenseText> text = ExpenseText.Create(description, supplier);

        if (text.IsFailure)
        {
            return Result.Failure(text.Error);
        }

        if (groupId == Guid.Empty)
        {
            return Result.Failure(ExpenseErrors.GroupRequired);
        }

        if (!IsValidDueDay(dueDay))
        {
            return Result.Failure(ExpenseErrors.DueDayInvalid);
        }

        Description = text.Value.Description;
        Supplier = text.Value.Supplier;
        GroupId = groupId;
        DueDay = dueDay;
        return Result.Success();
    }

    /// <summary>Muda o valor de uma despesa fixa a partir de um mês, mantendo o histórico dos meses anteriores.</summary>
    public Result ChangeAmountFrom(YearMonth month, decimal amount)
    {
        if (Kind != ExpenseKind.Fixed)
        {
            return Result.Failure(ExpenseErrors.NotFixed);
        }

        if (!MoneyRules.IsValid(amount))
        {
            return Result.Failure(ExpenseErrors.AmountInvalid);
        }

        if (month < StartMonth)
        {
            return Result.Failure(ExpenseErrors.ChangeBeforeStart);
        }

        ExpenseAmountChange? existing = _amountChanges.FirstOrDefault(change => change.FromMonth == month);

        if (existing is null)
        {
            _amountChanges.Add(new ExpenseAmountChange(month, amount));
        }
        else
        {
            existing.Amount = amount;
        }

        return Result.Success();
    }

    public Result SetInstallmentAmount(int number, decimal amount)
    {
        if (Kind != ExpenseKind.Installment)
        {
            return Result.Failure(ExpenseErrors.NotInstallment);
        }

        ExpenseInstallment? installment = _installments.FirstOrDefault(item => item.Number == number);

        if (installment is null)
        {
            return Result.Failure(ExpenseErrors.InstallmentNotFound);
        }

        if (!MoneyRules.IsValid(amount))
        {
            return Result.Failure(ExpenseErrors.AmountInvalid);
        }

        installment.Amount = amount;
        return Result.Success();
    }

    public Result End(YearMonth lastMonth)
    {
        if (Kind is not (ExpenseKind.Fixed or ExpenseKind.Variable))
        {
            return Result.Failure(ExpenseErrors.EndNotAllowed);
        }

        if (lastMonth < StartMonth)
        {
            return Result.Failure(ExpenseErrors.EndBeforeStart);
        }

        EndMonth = lastMonth;
        return Result.Success();
    }

    public bool IsDueIn(YearMonth month) => month >= StartMonth && (LastMonth is not { } last || month <= last);

    public bool IsEnded(YearMonth reference) => LastMonth is { } last && last < reference;

    /// <summary>Valor previsto para o mês. Nulo quando a despesa não vence no mês ou quando o valor é digitado todo mês (variável).</summary>
    public decimal? AmountFor(YearMonth month)
    {
        if (!IsDueIn(month))
        {
            return null;
        }

        return Kind switch
        {
            ExpenseKind.Variable => null,
            ExpenseKind.Installment => InstallmentFor(month)?.Amount,
            _ => _amountChanges.Where(change => change.FromMonth <= month).MaxBy(change => change.FromMonth)?.Amount,
        };
    }

    /// <summary>Data de vencimento da despesa em um mês. Dia 31 em setembro vira 30; em fevereiro, 28 ou 29.</summary>
    public DateOnly DueDateIn(YearMonth month) =>
        new(month.Year, month.Month, Math.Min(DueDay, DateTime.DaysInMonth(month.Year, month.Month)));

    public Result<ExpenseEntry> GenerateEntry(YearMonth month, DateTimeOffset now)
    {
        if (!IsDueIn(month))
        {
            return Result.Failure<ExpenseEntry>(ExpenseErrors.NotDueInMonth);
        }

        string? label = Kind == ExpenseKind.Installment
            ? string.Create(CultureInfo.InvariantCulture, $"{InstallmentNumberIn(month)}/{InstallmentCount}")
            : null;

        return Result.Success(ExpenseEntry.Create(this, month, AmountFor(month), label, DueDateIn(month), now));
    }

    private int InstallmentNumberIn(YearMonth month) => CurrentInstallment!.Value + StartMonth.MonthsUntil(month);

    private ExpenseInstallment? InstallmentFor(YearMonth month) =>
        _installments.FirstOrDefault(installment => installment.Number == InstallmentNumberIn(month));

    private static Result<Expense> CreateBase(
        Guid condominiumId, Guid groupId, string? description, string? supplier, ExpenseKind kind, YearMonth startMonth, int dueDay, DateTimeOffset now)
    {
        if (condominiumId == Guid.Empty)
        {
            return Result.Failure<Expense>(ExpenseErrors.CondominiumRequired);
        }

        if (groupId == Guid.Empty)
        {
            return Result.Failure<Expense>(ExpenseErrors.GroupRequired);
        }

        if (!IsValidDueDay(dueDay))
        {
            return Result.Failure<Expense>(ExpenseErrors.DueDayInvalid);
        }

        Result<ExpenseText> text = ExpenseText.Create(description, supplier);

        return text.IsFailure
            ? Result.Failure<Expense>(text.Error)
            : Result.Success(new Expense(condominiumId, groupId, text.Value, kind, startMonth, dueDay, now));
    }

    private static bool IsValidDueDay(int dueDay) => dueDay is >= 1 and <= 31;

    private sealed record ExpenseText(string Description, string? Supplier)
    {
        public static Result<ExpenseText> Create(string? description, string? supplier)
        {
            if (!TextRules.IsValidText(description, TextMaxLength, out string normalizedDescription))
            {
                return Result.Failure<ExpenseText>(ExpenseErrors.DescriptionInvalid);
            }

            string normalizedSupplier = TextRules.Normalize(supplier);

            if (normalizedSupplier.Length > TextMaxLength || TextRules.HasControlCharacters(supplier))
            {
                return Result.Failure<ExpenseText>(ExpenseErrors.SupplierInvalid);
            }

            return Result.Success(new ExpenseText(normalizedDescription, normalizedSupplier.Length == 0 ? null : normalizedSupplier));
        }
    }
}
