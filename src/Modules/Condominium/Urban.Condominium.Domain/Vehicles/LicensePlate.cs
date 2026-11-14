using Urban.SharedKernel;

namespace Urban.Condominium.Domain.Vehicles;

/// <summary>
/// A placa do veículo, nos dois formatos em uso no Brasil: o antigo (três letras e quatro números, ABC-1234) e o Mercosul
/// (três letras, um número, uma letra e dois números, ABC1D23). Guardada sempre em maiúsculas e sem hífen, para a mesma
/// placa digitada de jeitos diferentes ser uma só.
/// </summary>
public static class LicensePlate
{
    public const int Length = 7;

    private const string Separators = "- ";

    public static Result<string> Normalize(string? typed)
    {
        string plate = TextRules.RemoveAny(typed ?? string.Empty, Separators).ToUpperInvariant();

        return IsOldFormat(plate) || IsMercosulFormat(plate) ? Result.Success(plate) : Result.Failure<string>(VehicleErrors.PlateInvalid);
    }

    /// <summary>Se a placa (já normalizada) é do padrão Mercosul. Quem passa uma placa inválida recebe falso.</summary>
    public static bool IsMercosul(string plate) => IsMercosulFormat(plate);

    private static bool IsOldFormat(string plate) =>
        plate.Length == Length && plate[..3].All(char.IsAsciiLetterUpper) && plate[3..].All(TextRules.IsAsciiDigit);

    private static bool IsMercosulFormat(string plate) =>
        plate.Length == Length
        && plate[..3].All(char.IsAsciiLetterUpper)
        && TextRules.IsAsciiDigit(plate[3])
        && char.IsAsciiLetterUpper(plate[4])
        && TextRules.IsAsciiDigit(plate[5])
        && TextRules.IsAsciiDigit(plate[6]);
}
