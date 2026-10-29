namespace Urban.PostalCodeLookup;

internal static class PostalCodeFormat
{
    private const int Length = 8;

    /// <summary>O CEP entra no caminho da URL do provedor: só oito dígitos passam, para nada além disso viajar na requisição.</summary>
    /// <exception cref="ArgumentException">O CEP não tem oito dígitos.</exception>
    public static void EnsureValid(string postalCode)
    {
        if (postalCode is not { Length: Length } || !postalCode.All(char.IsAsciiDigit))
        {
            throw new ArgumentException("O CEP precisa ter exatamente oito dígitos.", nameof(postalCode));
        }
    }
}
