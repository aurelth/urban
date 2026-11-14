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
    /// <param name="underAge">O erro para quem ainda não tem 18 anos; o padrão fala do síndico, e quem cadastra outro papel manda o seu.</param>
    public static Result<BirthDate> Create(DateOnly? value, DateOnly today, Error? underAge = null)
    {
        Result<BirthDate> date = CreateAllowingMinor(value, today);

        return date.IsSuccess && date.Value.IsMinorOn(today) ? Result.Failure<BirthDate>(underAge ?? UnderAge) : date;
    }

    /// <summary>
    /// Como <see cref="Create"/>, mas aceita quem ainda não fez 18 anos: é o caso do dependente menor. Só a data é conferida
    /// (não no futuro, no máximo 120 anos); quem decide se um menor serve é a regra do vínculo.
    /// </summary>
    public static Result<BirthDate> CreateAllowingMinor(DateOnly? value, DateOnly today)
    {
        if (value is null || value > today || value < today.AddYears(-MaximumAge))
        {
            return Result.Failure<BirthDate>(Invalid);
        }

        return Result.Success(new BirthDate(value.Value));
    }

    /// <summary>Verdadeiro até o dia anterior ao 18º aniversário; no aniversário a pessoa já é adulta.</summary>
    public bool IsMinorOn(DateOnly today) => TurnsAgeOn(Value, MinimumAge) > today;

    /// <summary>Reconstitui uma data já aceita no cadastro, vinda do armazenamento. A regra de idade vale no momento do cadastro, não a cada leitura.</summary>
    public static BirthDate Restore(DateOnly value) => new(value);

    private static DateOnly TurnsAgeOn(DateOnly birth, int years)
    {
        bool leapDay = birth.Month == 2 && birth.Day == 29;

        return leapDay && !DateTime.IsLeapYear(birth.Year + years)
            ? new DateOnly(birth.Year + years, 3, 1)
            : birth.AddYears(years);
    }
}
