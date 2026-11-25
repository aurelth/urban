using System.Globalization;
using Urban.Finance.Domain.Payments;

namespace Urban.Finance.Application.Receipts;

/// <summary>Os textos do recibo em português do Brasil, escritos à mão como os da fatura (<c>InvoiceText</c>), para o PDF sair igual em qualquer máquina.</summary>
public static class ReceiptText
{
    /// <summary>O número como se fala dele: "REC-000123" (seis dígitos no mínimo; passando de 999.999 cresce sem cortar).</summary>
    public static string Code(int number) => $"REC-{Digits(number)}";

    public static string FileName(int number) => $"recibo-{Digits(number)}.pdf";

    public static string Method(PaymentMethod method) => method switch
    {
        PaymentMethod.Pix => "Pix",
        PaymentMethod.Boleto => "Boleto",
        PaymentMethod.Transfer => "Transferência",
        PaymentMethod.Cash => "Dinheiro",
        _ => "Outra forma",
    };

    private static string Digits(int number) => number.ToString("D6", CultureInfo.InvariantCulture);
}
