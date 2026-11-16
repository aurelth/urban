using Urban.SharedKernel;

namespace Urban.Finance.Domain.Payments;

public static class PaymentErrors
{
    public static readonly Error AmountInvalid = new("finance.payment.amount_invalid", "O valor do pagamento deve ser maior que zero, ter no máximo 2 casas decimais e não passar de R$ 9.999.999,99");

    public static readonly Error MethodInvalid = new("finance.payment.method_invalid", "Escolha a forma de pagamento: Pix, boleto, transferência, dinheiro ou outra");

    public static readonly Error DateInvalid = new("finance.payment.date_invalid", "A data de recebimento não pode ser futura nem anterior a 12 meses antes do mês da cobrança");

    public static readonly Error NoteInvalid = new("finance.payment.note_invalid", "A observação deve ter uma linha só, com até 200 caracteres");

    public static readonly Error ReasonInvalid = new("finance.payment.reason_invalid", "Informe o motivo do estorno, em uma linha só e com até 200 caracteres");

    public static readonly Error PageInvalid = new("finance.payment.page_invalid", "A página deve ser 1 ou maior e o tamanho da página deve ficar entre 1 e 200");

    public static readonly Error AlreadyReversed = new("finance.payment.already_reversed", "Este pagamento já foi estornado");

    public static readonly Error NotFound = new("finance.payment.not_found", "Pagamento não encontrado");

    public static readonly Error ChargeNotFound = new("finance.charge.not_found", "Cobrança não encontrada");

    /// <summary>O pagamento passaria do que ainda falta pagar. O saldo vai em <c>data.balance</c>, para a tela dizer quanto falta.</summary>
    public static Error ExceedsBalance(decimal balance) => new(
        "finance.payment.exceeds_balance",
        "O valor passa do que falta pagar nesta cobrança",
        new Dictionary<string, object?> { ["balance"] = balance });
}
