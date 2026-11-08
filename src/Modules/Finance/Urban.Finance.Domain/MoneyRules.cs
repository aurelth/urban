namespace Urban.Finance.Domain;

internal static class MoneyRules
{
    public const decimal Maximum = 9_999_999.99m;
    public const int DecimalPlaces = 2;

    public static bool IsValid(decimal amount) =>
        amount > 0 && amount <= Maximum && decimal.Round(amount, DecimalPlaces) == amount;
}
