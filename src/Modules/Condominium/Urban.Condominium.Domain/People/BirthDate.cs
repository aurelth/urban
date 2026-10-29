using Urban.SharedKernel;

namespace Urban.Condominium.Domain.People;

public sealed record BirthDate
{
    private const int MinimumAge = 18;
    private const int MaximumAge = 120;

    public static readonly Error Invalid = new("condominium.birth_date.invalid", "Informe uma data de nascimento válida");

    public static readonly Error UnderAge = new("condominium.birth_date.under_age", "O síndico precisa ter 18 anos ou mais");

    private BirthDate(DateOnly value)
    {
        Value = value;
    }

    public DateOnly Value { get; }

    /// <param name="value">Data informada.</param>
    /// <param name="today">Data de hoje, recebida de fora para a regra continuar pura e testável.</param>
    public static Result<BirthDate> Create(DateOnly? value, DateOnly today)
    {
        if (value is null || value > today || value < today.AddYears(-MaximumAge))
        {
            return Result.Failure<BirthDate>(Invalid);
        }

        // AddYears leva 29 de fevereiro para 28 de fevereiro nos anos comuns; a regra civil usa 1º de março.
        DateOnly eighteenthBirthday = TurnsAgeOn(value.Value, MinimumAge);

        return eighteenthBirthday <= today
            ? Result.Success(new BirthDate(value.Value))
            : Result.Failure<BirthDate>(UnderAge);
    }

    private static DateOnly TurnsAgeOn(DateOnly birth, int years)
    {
        bool leapDay = birth.Month == 2 && birth.Day == 29;

        return leapDay && !DateTime.IsLeapYear(birth.Year + years)
            ? new DateOnly(birth.Year + years, 3, 1)
            : birth.AddYears(years);
    }
}
