using Urban.SharedKernel;

namespace Urban.Finance.Domain.Expenses;

public static class InstallmentPlan
{
    public const int MinimumCount = 2;
    public const int MaximumCount = 120;

    /// <summary>Divide o total em parcelas iguais de centavos inteiros; a sobra de centavos vai para a última parcela.</summary>
    public static Result<IReadOnlyList<decimal>> Split(decimal total, int count)
    {
        if (!MoneyRules.IsValid(total))
        {
            return Result.Failure<IReadOnlyList<decimal>>(ExpenseErrors.AmountInvalid);
        }

        if (count is < MinimumCount or > MaximumCount)
        {
            return Result.Failure<IReadOnlyList<decimal>>(ExpenseErrors.InstallmentCountInvalid);
        }

        decimal regular = decimal.Floor(total / count * 100m) / 100m;

        if (regular <= 0)
        {
            return Result.Failure<IReadOnlyList<decimal>>(ExpenseErrors.AmountInvalid);
        }

        List<decimal> amounts = [.. Enumerable.Repeat(regular, count - 1), total - (regular * (count - 1))];
        return Result.Success<IReadOnlyList<decimal>>(amounts);
    }
}
