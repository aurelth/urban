using System.Globalization;
using System.Text;
using Urban.Finance.Domain.Months;
using Urban.Finance.Domain.Payments;
using Urban.SharedKernel.Contracts;

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

    /// <summary>Porcentagem de 0 a 100 com até 4 casas e sem zeros à direita: "60%", "2,5%", "33,3333%".</summary>
    public static string Percent(decimal percent) => percent.ToString("#,##0.####", Brazilian) + "%";

    public static string Method(ApportionmentBasisKind kind) => kind switch
    {
        ApportionmentBasisKind.IdealFraction => "Fração ideal",
        ApportionmentBasisKind.EqualShare => "Partes iguais",
        _ => "Área",
    };

    /// <summary>A despesa como aparece no demonstrativo: o nome e, se for parcela, o número dela entre parênteses.</summary>
    public static string ExpenseItem(string description, string? installment) =>
        installment is null ? description : $"{description} ({installment})";

    public static string PixKind(PixKeyKind kind) => kind switch
    {
        PixKeyKind.Cnpj => "CNPJ",
        PixKeyKind.Cpf => "CPF",
        PixKeyKind.Email => "E-mail",
        PixKeyKind.Phone => "Celular",
        _ => "Chave aleatória",
    };

    /// <summary>A chave Pix como se escreve no dia a dia. Forma que não confere com o tipo fica como veio, sem quebrar a fatura.</summary>
    public static string PixKey(PixKeyKind kind, string key) => kind switch
    {
        PixKeyKind.Cnpj when key.Length == 14 => $"{key[..2]}.{key[2..5]}.{key[5..8]}/{key[8..12]}-{key[12..]}",
        PixKeyKind.Cpf when key.Length == 11 => $"{key[..3]}.{key[3..6]}.{key[6..9]}-{key[9..]}",
        PixKeyKind.Phone when key.Length == 14 && key.StartsWith("+55", StringComparison.Ordinal) => $"+55 ({key[3..5]}) {key[5..10]}-{key[10..]}",
        _ => key,
    };

    public static string AccountKind(BankAccountKind kind) => kind == BankAccountKind.Savings ? "Conta poupança" : "Conta corrente";

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
