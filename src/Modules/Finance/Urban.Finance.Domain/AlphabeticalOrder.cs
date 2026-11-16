using System.Globalization;
using System.Text;

namespace Urban.Finance.Domain;

/// <summary>Ordem alfabética sem diferenciar maiúsculas nem acentos ("água" entre "A" e "B"), igual em qualquer máquina: não depende da cultura do sistema. Feita em memória: as listas são pequenas.</summary>
public static class AlphabeticalOrder
{
    public static readonly IComparer<string> Comparer = Comparer<string>.Create(Compare);

    private static int Compare(string? left, string? right)
    {
        int byKey = string.CompareOrdinal(Key(left), Key(right));

        return byKey != 0 ? byKey : string.CompareOrdinal(left, right);
    }

    private static string Key(string? text)
    {
        StringBuilder key = new();

        foreach (char c in (text ?? string.Empty).Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                key.Append(char.ToLowerInvariant(c));
            }
        }

        return key.ToString();
    }
}
