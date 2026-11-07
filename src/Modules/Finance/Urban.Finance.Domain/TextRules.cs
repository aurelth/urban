namespace Urban.Finance.Domain;

internal static class TextRules
{
    public static string Normalize(string? value) =>
        string.Join(' ', (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public static bool HasControlCharacters(string? value) => value is not null && value.Any(char.IsControl);

    public static bool IsValidText(string? raw, int maxLength, out string normalized)
    {
        normalized = Normalize(raw);
        return normalized.Length > 0 && normalized.Length <= maxLength && !HasControlCharacters(raw);
    }
}
