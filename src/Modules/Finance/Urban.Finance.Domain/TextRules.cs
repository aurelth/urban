using System.Globalization;
using System.Text;

namespace Urban.Finance.Domain;

internal static class TextRules
{
    /// <summary>Ordem alfabética sem diferenciar maiúsculas nem acentos ("água" entre "A" e "B"), igual em qualquer máquina: não depende da cultura do sistema.</summary>
    public static readonly IComparer<string> Alphabetical = Comparer<string>.Create(CompareAlphabetically);

    public static string Normalize(string? value) =>
        string.Join(' ', (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public static bool HasControlCharacters(string? value) => value is not null && value.Any(char.IsControl);

    public static bool IsValidText(string? raw, int maxLength, out string normalized)
    {
        normalized = Normalize(raw);
        return normalized.Length > 0 && normalized.Length <= maxLength && !HasControlCharacters(raw);
    }

    private static int CompareAlphabetically(string? left, string? right)
    {
        int byKey = string.CompareOrdinal(AlphabeticalKey(left), AlphabeticalKey(right));

        return byKey != 0 ? byKey : string.CompareOrdinal(left, right);
    }

    private static string AlphabeticalKey(string? text)
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
