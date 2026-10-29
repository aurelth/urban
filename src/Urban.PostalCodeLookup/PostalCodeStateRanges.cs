using System.Globalization;

namespace Urban.PostalCodeLookup;

/// <summary>
/// Faixas de CEP por estado, como os Correios as distribuem. Serve para desconfiar de um provedor: se ele devolve uma UF que não é
/// a dona da faixa do CEP (como Sarandi, PR, para o 99999-999, que é do Rio Grande do Sul), a resposta não é usada. Todas as
/// faixas começam e terminam em fronteiras de cinco dígitos, então basta comparar o prefixo do CEP.
/// </summary>
public static class PostalCodeStateRanges
{
    private const int PrefixLength = 5;

    // AM, DF e GO têm duas faixas cada, porque RR e as áreas vizinhas do DF e de GO ficam no meio delas.
    private static readonly StateRange[] Ranges =
    [
        new("SP", 01000, 19999),
        new("RJ", 20000, 28999),
        new("ES", 29000, 29999),
        new("MG", 30000, 39999),
        new("BA", 40000, 48999),
        new("SE", 49000, 49999),
        new("PE", 50000, 56999),
        new("AL", 57000, 57999),
        new("PB", 58000, 58999),
        new("RN", 59000, 59999),
        new("CE", 60000, 63999),
        new("PI", 64000, 64999),
        new("MA", 65000, 65999),
        new("PA", 66000, 68899),
        new("AP", 68900, 68999),
        new("AM", 69000, 69299),
        new("RR", 69300, 69399),
        new("AM", 69400, 69899),
        new("AC", 69900, 69999),
        new("DF", 70000, 72799),
        new("GO", 72800, 72999),
        new("DF", 73000, 73699),
        new("GO", 73700, 76799),
        new("RO", 76800, 76999),
        new("TO", 77000, 77999),
        new("MT", 78000, 78899),
        new("MS", 79000, 79999),
        new("PR", 80000, 87999),
        new("SC", 88000, 89999),
        new("RS", 90000, 99999),
    ];

    /// <summary>Diz se a UF (sem diferenciar maiúsculas de minúsculas) é a dona da faixa em que o CEP está.</summary>
    /// <exception cref="ArgumentException">O CEP não tem oito dígitos.</exception>
    public static bool Matches(string postalCode, string state)
    {
        PostalCodeFormat.EnsureValid(postalCode);

        int prefix = int.Parse(postalCode.AsSpan(0, PrefixLength), CultureInfo.InvariantCulture);

        return Ranges.Any(range =>
            prefix >= range.First
            && prefix <= range.Last
            && string.Equals(range.State, state, StringComparison.OrdinalIgnoreCase));
    }

    private readonly record struct StateRange(string State, int First, int Last);
}
