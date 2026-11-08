namespace Urban.Finance.Domain.Consumption;

/// <summary>O fundo de reserva é um percentual sobre as despesas dos grupos que o condomínio marcou como base dele.</summary>
public static class ReserveFund
{
    public static bool IsValidPercent(decimal percent) =>
        percent is >= 0 and <= 100 && MoneyRules.HasAtMostDecimals(percent, 2);

    public static decimal AmountFor(decimal baseTotal, decimal percent) => MoneyRules.Round(baseTotal * percent / 100m);
}
