using Urban.Finance.Application.Invoices;
using Urban.Finance.Domain.Months;
using Urban.Finance.Domain.Payments;
using Urban.SharedKernel.Contracts;

namespace Urban.Finance.Application.Receipts;

/// <summary>O e-mail que leva o recibo em anexo. Texto simples, escrito à mão como o da fatura, para sair igual em qualquer máquina.</summary>
public static class ReceiptEmail
{
    public static string Subject(Payment payment, InvoiceCondominium condominium) =>
        $"Recibo {ReceiptText.Code(payment.ReceiptNumber)} - {condominium.Name}";

    public static string Body(Payment payment, string unit, YearMonth month, InvoiceCondominium condominium, InvoiceResponsible responsible) =>
        $"""
        Olá, {responsible.Name}.

        Segue em anexo o recibo {ReceiptText.Code(payment.ReceiptNumber)} do pagamento da fatura de {InvoiceText.Competence(month)} da unidade {unit}, do condomínio {condominium.Name}.

        Valor recebido: {InvoiceText.Money(payment.Amount)}
        Data do recebimento: {InvoiceText.Date(payment.ReceivedOn)}
        Forma de pagamento: {ReceiptText.Method(payment.Method)}

        {InvoiceEmail.Contact(condominium.Manager, "o recibo")}

        Esta mensagem é automática: não responda a este e-mail.

        Urban
        """;
}
