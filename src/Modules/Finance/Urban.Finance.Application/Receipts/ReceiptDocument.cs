using Urban.Finance.Domain.Months;
using Urban.Finance.Domain.Payments;
using Urban.SharedKernel.Contracts;

namespace Urban.Finance.Application.Receipts;

/// <summary>
/// Tudo o que o PDF do recibo mostra, já decidido: o renderizador só desenha. É o comprovante de um pagamento, então o que vem
/// da cobrança é o que ela era quando fechou, e o que vem do pagamento é o que o síndico registrou. A observação do pagamento é
/// anotação interna e nunca entra aqui.
/// </summary>
/// <param name="Code">O número do recibo como se fala dele, "REC-000123".</param>
/// <param name="ResponsibleName">O responsável da unidade hoje; nulo quando a unidade não tem.</param>
/// <param name="InvoiceReference">A referência da fatura paga (a mesma impressa nela), para ligar um papel ao outro.</param>
/// <param name="ChargeTotal">O valor da cobrança.</param>
/// <param name="Amount">O valor deste pagamento.</param>
/// <param name="PaidSoFar">O que estava pago da cobrança ao receber este pagamento, ele incluído (sem os estornados); só os pagamentos com número igual ou menor que o dele contam.</param>
/// <param name="Balance">O que faltava pagar depois deste pagamento; zero quando a cobrança ficou quitada.</param>
/// <param name="IssuedOn">O dia em que o pagamento foi registrado e o recibo emitido, em Brasília.</param>
/// <param name="CanceledOn">O dia do estorno, em Brasília; nulo enquanto o pagamento vale. O motivo do estorno não entra no recibo.</param>
/// <param name="Manager">Nulo quando o cadastro do síndico não foi encontrado.</param>
public sealed record ReceiptDocument(
    string Code,
    string CondominiumName,
    string CondominiumCnpj,
    string CondominiumAddress,
    string UnitLabel,
    string? ResponsibleName,
    YearMonth Month,
    string InvoiceReference,
    decimal ChargeTotal,
    decimal Amount,
    DateOnly ReceivedOn,
    PaymentMethod Method,
    decimal PaidSoFar,
    decimal Balance,
    DateOnly IssuedOn,
    DateOnly? CanceledOn,
    InvoiceManager? Manager);

/// <param name="FileName">Nome seguro para o download (sem pasta, acento ou espaço).</param>
public sealed record ReceiptFile(string FileName, byte[] Content)
{
    public override string ToString() => $"{nameof(ReceiptFile)} {{ {nameof(FileName)} = {FileName}, Bytes = {Content.Length} }}";
}
