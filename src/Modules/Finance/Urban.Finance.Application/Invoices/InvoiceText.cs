using System.Globalization;
using System.Text;
using Urban.Finance.Domain.Months;

namespace Urban.Finance.Application.Invoices;

/// <summary>
/// Os textos da fatura em português do Brasil. Tudo é escrito à mão, sem <see cref="CultureInfo"/> do servidor nem normalização do
/// ICU, para o PDF sair igual em qualquer máquina e imagem.
/// </summary>
public static class InvoiceText
{
    private static readonly NumberFormatInfo Brazilian = new() { NumberDecimalSeparator = ",", NumberGroupSeparator = ".", NumberGroupSizes = [3] };

    private static readonly string[] MonthNames =
    [
        "Janeiro", "Fevereiro", "Março", "Abril", "Maio", "Junho", "Julho", "Agosto", "Setembro", "Outubro", "Novembro", "Dezembro",
    ];

    private static readonly (string Letters, char Plain)[] Folding =
    [
        ("àáâãäåÀÁÂÃÄÅ", 'a'),
        ("çÇ", 'c'),
        ("èéêëÈÉÊË", 'e'),
        ("ìíîïÌÍÎÏ", 'i'),
        ("ñÑ", 'n'),
        ("òóôõöÒÓÔÕÖ", 'o'),
        ("ùúûüÙÚÛÜ", 'u'),
        ("ýÿÝ", 'y'),
    ];

    public static string Money(decimal amount) => "R$ " + Math.Round(amount, 2, MidpointRounding.AwayFromZero).ToString("#,##0.00", Brazilian);

    /// <summary>Consumo ou leitura em m³, com até 3 casas e sem zeros à direita.</summary>
    public static string Cubic(decimal amount) => amount.ToString("#,##0.###", Brazilian);

    public static string Competence(YearMonth month) => $"{MonthNames[month.Month - 1]} de {month.Year}";

    public static string Date(DateOnly date) => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    /// <summary>O mesmo rótulo das telas: "Bloco A · 102", ou só o número quando a unidade não tem bloco.</summary>
    public static string Unit(string? block, string number) => string.IsNullOrWhiteSpace(block) ? number : $"Bloco {block} · {number}";

    public static string FileName(YearMonth month, string? block, string number) =>
        string.IsNullOrWhiteSpace(block)
            ? $"fatura-{month}-{Slug(number)}.pdf"
            : $"fatura-{month}-bloco-{Slug(block)}-{Slug(number)}.pdf";

    public static string Reference(YearMonth month, Guid chargeId) => $"{month}-{chargeId.ToString("N")[^8..].ToUpperInvariant()}";

    private static string Slug(string text)
    {
        StringBuilder slug = new(text.Length);
        bool pendingDash = false;

        foreach (char letter in text)
        {
            char plain = Fold(letter);

            if (plain is (>= 'a' and <= 'z') or (>= '0' and <= '9'))
            {
                if (pendingDash && slug.Length > 0)
                {
                    slug.Append('-');
                }

                slug.Append(plain);
                pendingDash = false;
            }
            else
            {
                pendingDash = true;
            }
        }

        return slug.ToString();
    }

    private static char Fold(char letter)
    {
        foreach ((string letters, char plain) in Folding)
        {
            if (letters.Contains(letter, StringComparison.Ordinal))
            {
                return plain;
            }
        }

        return char.ToLowerInvariant(letter);
    }
}
