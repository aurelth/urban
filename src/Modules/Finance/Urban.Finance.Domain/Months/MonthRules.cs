using Urban.SharedKernel;

namespace Urban.Finance.Domain.Months;

public static class MonthRules
{
    /// <summary>Quantos meses à frente do atual um mês pode ser aberto (5 anos).</summary>
    public const int MaxMonthsAhead = 60;

    public static Result CanOpen(YearMonth month, DateOnly today) =>
        month > YearMonth.From(today).AddMonths(MaxMonthsAhead)
            ? Result.Failure(MonthErrors.TooFarAhead)
            : Result.Success();
}
