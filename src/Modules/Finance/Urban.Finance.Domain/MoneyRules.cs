namespace Urban.Finance.Domain;

internal static class MoneyRules
{
    public const decimal Maximum = 9_999_999.99m;
    public const int DecimalPlaces = 2;

    public static bool IsValid(decimal amount) =>
        amount > 0 && amount <= Maximum && decimal.Round(amount, DecimalPlaces) == amount;

    /// <summary>Arredonda para centavos: o meio centavo sobe, como no boleto.</summary>
    public static decimal Round(decimal amount) => decimal.Round(amount, DecimalPlaces, MidpointRounding.AwayFromZero);

    /// <summary>Verdadeiro quando o número não tem mais casas decimais do que o permitido.</summary>
    public static bool HasAtMostDecimals(decimal value, int places) => decimal.Round(value, places) == value;
}
