using Urban.Finance.Domain.Months;
using Urban.Finance.Domain.Payments;
using Urban.SharedKernel.Contracts;

namespace Urban.Finance.Application.Receipts;

/// <summary>O e-mail que leva o recibo em anexo. Texto simples, escrito à mão como o da fatura, para sair igual em qualquer máquina.</summary>
public static class ReceiptEmail
{
    public static string Subject(Payment payment, InvoiceCondominium condominium) => $"{payment}{condominium}";

    public static string Body(Payment payment, string unit, YearMonth month, InvoiceCondominium condominium, InvoiceResponsible responsible) =>
        $"{payment}{unit}{month}{condominium}{responsible}";
}
