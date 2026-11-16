using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Months;
using Urban.Finance.Domain.Payments;
using Urban.SharedKernel.Contracts;

namespace Urban.Finance.Application.Invoices;

/// <param name="Quantity">Consumo em m³ (água, esgoto e gás); nulo nas demais linhas.</param>
public sealed record InvoiceLine(ChargeLineKind Kind, string Label, decimal Amount, decimal? Quantity, decimal? PreviousReading, decimal? CurrentReading);

/// <param name="Status">Aberta, paga em parte, paga ou vencida, na data em que a fatura foi pedida.</param>
/// <param name="Paid">O que já foi pago (estornos não contam).</param>
/// <param name="Balance">O que falta pagar.</param>
public sealed record InvoiceSituation(ChargeStatus Status, decimal Paid, decimal Balance);

/// <param name="Method">Em que o rateio se baseou naquele mês.</param>
/// <param name="Percent">A participação da unidade, de 0 a 100: o peso dela dividido pelo peso de todas as unidades do fechamento.</param>
public sealed record InvoiceShare(ApportionmentBasisKind Method, decimal Percent);

/// <summary>Uma linha do demonstrativo: o quanto o condomínio todo gastou naquele grupo (ou colocou no fundo de reserva) no mês.</summary>
public sealed record InvoiceStatementLine(string Label, decimal Amount);

/// <summary>Uma cobrança de mês anterior da mesma unidade que ainda tem saldo.</summary>
public sealed record InvoiceEarlierCharge(YearMonth Month, DateOnly DueDate, decimal Amount, decimal Paid)
{
    public decimal Balance => Amount - Paid;
}

/// <param name="KindLabel">O tipo da chave por extenso: CNPJ, CPF, E-mail, Celular ou Chave aleatória.</param>
/// <param name="Key">A chave como as pessoas a escrevem (CNPJ e CPF com pontuação, celular com +55).</param>
public sealed record InvoicePix(string KindLabel, string Key);

public sealed record InvoiceAccount(string Bank, string Agency, string Number, string KindLabel);

/// <summary>Como pagar: a chave Pix e/ou a conta, o que o condomínio tiver cadastrado. Nunca os dois vazios.</summary>
public sealed record InvoiceHowToPay(InvoicePix? Pix, InvoiceAccount? Account);

/// <summary>Tudo o que o PDF mostra, já decidido: o renderizador só desenha.</summary>
/// <param name="Reference">Código curto para o síndico e o morador falarem da mesma fatura.</param>
/// <param name="ResponsibleName">Quem recebe a fatura; nulo quando a unidade não tem responsável.</param>
/// <param name="IssuedOn">Dia do fechamento do mês, em Brasília.</param>
/// <param name="Statement">O demonstrativo dos gastos do condomínio no mês: os grupos de despesa e o fundo de reserva (o consumo individual de água, esgoto e gás não entra).</param>
/// <param name="Earlier">Cobranças de meses anteriores da unidade que ainda têm saldo, da mais antiga para a mais nova.</param>
/// <param name="HowToPay">Nulo quando o condomínio ainda não cadastrou Pix nem conta.</param>
/// <param name="Manager">Nulo quando o cadastro do síndico não foi encontrado.</param>
public sealed record InvoiceDocument(
    string Reference,
    string CondominiumName,
    string CondominiumCnpj,
    string CondominiumAddress,
    string UnitLabel,
    string? ResponsibleName,
    YearMonth Month,
    DateOnly DueDate,
    DateOnly IssuedOn,
    IReadOnlyList<InvoiceLine> Lines,
    decimal Total,
    InvoiceSituation Situation,
    InvoiceShare Share,
    IReadOnlyList<InvoiceStatementLine> Statement,
    IReadOnlyList<InvoiceEarlierCharge> Earlier,
    InvoiceHowToPay? HowToPay,
    InvoiceManager? Manager)
{
    public decimal StatementTotal => Statement.Sum(line => line.Amount);

    public decimal EarlierBalance => Earlier.Sum(charge => charge.Balance);
}

/// <param name="FileName">Nome seguro para o download (sem pasta, acento ou espaço).</param>
public sealed record InvoiceFile(string FileName, byte[] Content)
{
    public override string ToString() => $"{nameof(InvoiceFile)} {{ {nameof(FileName)} = {FileName}, Bytes = {Content.Length} }}";
}
