namespace Urban.Condominium.Domain.Units;

/// <summary>
/// A ordem em que as unidades aparecem: primeiro o bloco (quem não tem bloco vem antes), depois o número, em ordem
/// "natural" (2 antes de 10, Casa 2 antes de Casa 10) e sem diferenciar maiúsculas de minúsculas. A listagem pelo banco
/// usa uma collation com a mesma ideia; este comparador vale para quem ordena em memória.
/// </summary>
public static class UnitOrder
{
    public static readonly IComparer<CondominiumUnit> Comparer = new UnitComparer();

    /// <summary>Compara dois textos pela ordem natural, ignorando maiúsculas; empates saem por ordem ordinal, para o resultado não depender da entrada.</summary>
    public static int CompareNatural(string? left, string? right)
    {
        string a = left ?? string.Empty;
        string b = right ?? string.Empty;
        int i = 0;
        int j = 0;

        while (i < a.Length && j < b.Length)
        {
            int step = char.IsAsciiDigit(a[i]) && char.IsAsciiDigit(b[j]) ? CompareNumberRuns(a, ref i, b, ref j) : CompareChars(a[i++], b[j++]);
            if (step != 0)
            {
                return step;
            }
        }

        int remaining = (a.Length - i).CompareTo(b.Length - j);

        return remaining != 0 ? remaining : string.CompareOrdinal(a, b);
    }

    private static int CompareChars(char left, char right) => char.ToUpperInvariant(left).CompareTo(char.ToUpperInvariant(right));

    /// <summary>Compara a sequência de dígitos que começa em cada posição pelo valor (sem zeros à esquerda) e avança as duas posições.</summary>
    private static int CompareNumberRuns(string a, ref int i, string b, ref int j)
    {
        ReadOnlySpan<char> first = DigitRun(a, ref i);
        ReadOnlySpan<char> second = DigitRun(b, ref j);

        int byLength = first.Length.CompareTo(second.Length);

        return byLength != 0 ? byLength : first.SequenceCompareTo(second);
    }

    private static ReadOnlySpan<char> DigitRun(string text, ref int index)
    {
        int start = index;
        while (index < text.Length && char.IsAsciiDigit(text[index]))
        {
            index++;
        }

        return text.AsSpan(start, index - start).TrimStart('0');
    }

    private sealed class UnitComparer : IComparer<CondominiumUnit>
    {
        public int Compare(CondominiumUnit? x, CondominiumUnit? y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            if (x is null)
            {
                return -1;
            }

            if (y is null)
            {
                return 1;
            }

            int byBlock = CompareNatural(x.Block, y.Block);

            return byBlock != 0 ? byBlock : CompareNatural(x.Number, y.Number);
        }
    }
}
