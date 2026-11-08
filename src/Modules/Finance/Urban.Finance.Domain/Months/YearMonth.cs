using System.Globalization;
using Urban.SharedKernel;

namespace Urban.Finance.Domain.Months;

/// <summary>Competência: um mês de um ano, sem dia. É o eixo de tudo no financeiro (despesas, leituras, cobranças).</summary>
public readonly record struct YearMonth : IComparable<YearMonth>
{
    public const int MinimumYear = 2000;
    public const int MaximumYear = 2200;

    private YearMonth(int year, int month)
    {
        Year = year;
        Month = month;
    }

    public int Year { get; }

    public int Month { get; }

    public DateOnly FirstDay => new(Year, Month, 1);

    public static Result<YearMonth> Create(int year, int month) =>
        year is < MinimumYear or > MaximumYear || month is < 1 or > 12
            ? Result.Failure<YearMonth>(MonthErrors.Invalid)
            : Result.Success(new YearMonth(year, month));

    public static Result<YearMonth> Parse(string? text)
    {
        string? value = text?.Trim();

        if (value is not { Length: 7 } || value[4] != '-')
        {
            return Result.Failure<YearMonth>(MonthErrors.Invalid);
        }

        bool hasYear = int.TryParse(value.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out int year);
        bool hasMonth = int.TryParse(value.AsSpan(5, 2), NumberStyles.None, CultureInfo.InvariantCulture, out int month);

        return hasYear && hasMonth
            ? Create(year, month)
            : Result.Failure<YearMonth>(MonthErrors.Invalid);
    }

    public static YearMonth From(DateOnly date) => new(date.Year, date.Month);

    public YearMonth AddMonths(int months)
    {
        int index = (Year * 12) + (Month - 1) + months;
        return new YearMonth(index / 12, (index % 12) + 1);
    }

    public int MonthsUntil(YearMonth other) => ((other.Year - Year) * 12) + (other.Month - Month);

    public int CompareTo(YearMonth other) => MonthsUntil(other) switch
    {
        > 0 => -1,
        < 0 => 1,
        _ => 0,
    };

    public static bool operator <(YearMonth left, YearMonth right) => left.CompareTo(right) < 0;

    public static bool operator >(YearMonth left, YearMonth right) => left.CompareTo(right) > 0;

    public static bool operator <=(YearMonth left, YearMonth right) => left.CompareTo(right) <= 0;

    public static bool operator >=(YearMonth left, YearMonth right) => left.CompareTo(right) >= 0;

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Year:0000}-{Month:00}");
}
