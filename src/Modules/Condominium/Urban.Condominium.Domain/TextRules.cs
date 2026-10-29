namespace Urban.Condominium.Domain;

internal static class TextRules
{
    public static string Normalize(string? value) =>
        string.Join(' ', (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public static bool HasControlCharacters(string value) => value.Any(char.IsControl);

    public static bool IsAsciiDigit(char c) => c is >= '0' and <= '9';

    public static string RemoveAny(string value, string characters) =>
        string.Concat(value.Where(c => !char.IsWhiteSpace(c) && !characters.Contains(c)));
}
