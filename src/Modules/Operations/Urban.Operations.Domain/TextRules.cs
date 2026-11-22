namespace Urban.Operations.Domain;

internal static class TextRules
{
    /// <summary>Junta as palavras com um espaço só e tira as pontas: o texto de uma linha (título, bloco).</summary>
    public static string Normalize(string? value) =>
        string.Join(' ', (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public static bool HasControlCharacters(string? value) => value is not null && value.Any(char.IsControl);

    /// <summary>Texto de uma linha só: sem quebra de linha nem outro caractere de controle.</summary>
    public static bool IsValidLine(string? raw, int maxLength, out string normalized)
    {
        normalized = Normalize(raw);

        return normalized.Length > 0 && normalized.Length <= maxLength && !HasControlCharacters(raw);
    }

    /// <summary>
    /// Texto de vários parágrafos: guarda as quebras de linha (sempre como <c>\n</c>) e as tabulações, tira os espaços das pontas e recusa
    /// qualquer outro caractere de controle.
    /// </summary>
    public static bool IsValidParagraphs(string? raw, int maxLength, out string normalized)
    {
        normalized = (raw ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Trim();

        return normalized.Length > 0 && normalized.Length <= maxLength && !normalized.Any(c => char.IsControl(c) && c is not ('\n' or '\t'));
    }
}
