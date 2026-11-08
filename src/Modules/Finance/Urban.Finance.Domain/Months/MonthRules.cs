using Urban.SharedKernel;

namespace Urban.Finance.Domain.Months;

public static class MonthRules
{
    public static Result CanOpen(YearMonth month, DateOnly today) =>
        month > YearMonth.From(today).AddMonths(1)
            ? Result.Failure(MonthErrors.TooFarAhead)
            : Result.Success();
}
