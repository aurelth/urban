using Urban.Finance.Application.Abstractions;
using Urban.SharedKernel.Contracts;

namespace Urban.Finance.Application.Invoices;

/// <summary>O e-mail que leva a fatura em anexo. Texto simples, escrito à mão como o resto da fatura, para sair igual em qualquer máquina.</summary>
public static class InvoiceEmail
{
    public static string Subject(ClosingCharge charge, InvoiceCondominium condominium) =>
        $"Fatura de {InvoiceText.Competence(charge.Month)} - {condominium.Name}";

    public static string Body(ClosingCharge charge, InvoiceCondominium condominium, InvoiceResponsible responsible) =>
        $"""
        Olá, {responsible.Name}.

        Segue em anexo a fatura de {InvoiceText.Competence(charge.Month)} da unidade {InvoiceText.Unit(charge.Block, charge.Number)}, do condomínio {condominium.Name}.

        Valor: {InvoiceText.Money(charge.Amount)}
        Vencimento: {InvoiceText.Date(charge.DueDate)}

        {Contact(condominium.Manager)}

        Esta mensagem é automática: não responda a este e-mail.

        Urban
        """;

    private static string Contact(InvoiceManager? manager) => manager switch
    {
        null => "Dúvidas sobre a fatura? Fale com o síndico do condomínio.",
        { Phone: { Length: > 0 } phone } => $"Dúvidas sobre a fatura? Fale com o síndico, {manager.Name}, pelo telefone {phone}.",
        _ => $"Dúvidas sobre a fatura? Fale com o síndico, {manager.Name}.",
    };
}
