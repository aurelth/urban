using Urban.SharedKernel;

namespace Urban.Finance.Domain.Closing;

public static class ChargeSplitter
{
    /// <summary>
    /// Reparte o total entre as cotas na proporção dos pesos, em centavos inteiros e somando exatamente o total.
    /// Método do maior resto: cada cota recebe o piso em centavos e os centavos que sobram vão, um a um,
    /// para as maiores partes fracionárias (empate: vence a cota que vem primeiro).
    /// </summary>
    public static Result<IReadOnlyList<decimal>> Split(decimal total, IReadOnlyList<decimal> weights)
    {
        if (total < 0 || total > MoneyRules.Maximum || decimal.Round(total, MoneyRules.DecimalPlaces) != total)
        {
            return Result.Failure<IReadOnlyList<decimal>>(ClosingErrors.TotalInvalid);
        }

        decimal weightSum = weights.Sum();

        if (weights.Count == 0 || weights.Any(weight => weight < 0) || weightSum <= 0)
        {
            return Result.Failure<IReadOnlyList<decimal>>(ClosingErrors.NoWeights);
        }

        long totalCents = (long)(total * 100m);
        decimal[] exact = [.. weights.Select(weight => totalCents * weight / weightSum)];
        long[] cents = [.. exact.Select(share => (long)decimal.Floor(share))];
        long leftover = totalCents - cents.Sum();

        foreach (int index in Enumerable.Range(0, exact.Length)
                     .OrderByDescending(index => exact[index] - cents[index])
                     .ThenBy(index => index)
                     .Take((int)leftover))
        {
            cents[index]++;
        }

        return Result.Success<IReadOnlyList<decimal>>([.. cents.Select(value => value / 100m)]);
    }
}
