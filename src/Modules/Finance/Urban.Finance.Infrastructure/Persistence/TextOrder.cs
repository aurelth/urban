using System.Globalization;
using System.Text;

namespace Urban.Finance.Infrastructure.Persistence;

/// <summary>Ordem alfabética sem diferenciar maiúsculas nem acentos ("água" entre "A" e "B"). Feita em memória: as listas são pequenas.</summary>
internal static class TextOrder
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
