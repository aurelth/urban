using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Months;
using Urban.SharedKernel.Contracts;

namespace Urban.Finance.Application.Invoices;

/// <param name="Quantity">Consumo em m³ (água, esgoto e gás); nulo nas demais linhas.</param>
public sealed record InvoiceLine(ChargeLineKind Kind, string Label, decimal Amount, decimal? Quantity, decimal? PreviousReading, decimal? CurrentReading);

/// <param name="Method">Em que o rateio se baseou naquele mês.</param>
/// <param name="Percent">A participação da unidade, de 0 a 100: o peso dela dividido pelo peso de todas as unidades do fechamento.</param>
public sealed record InvoiceShare(ApportionmentBasisKind Method, decimal Percent);

/// <summary>Uma despesa do mês, como o condomínio a lançou; parcela aparece no texto, como "Portão da garagem (4/6)".</summary>
public sealed record InvoiceStatementItem(string Description, decimal Amount);

/// <summary>As despesas de um grupo, em ordem alfabética, com o subtotal.</summary>
public sealed record InvoiceStatementGroup(string Name, IReadOnlyList<InvoiceStatementItem> Items)
{
    public decimal Subtotal => Items.Sum(item => item.Amount);
}

/// <summary>O que o condomínio todo gastou no mês, despesa por despesa, e o fundo de reserva (nulo quando o mês não tem).</summary>
public sealed record InvoiceStatement(IReadOnlyList<InvoiceStatementGroup> Groups, decimal? ReserveFund)
{
    public decimal Total => Groups.Sum(group => group.Subtotal) + (ReserveFund ?? 0m);
}

/// <param name="KindLabel">O tipo da chave por extenso: CNPJ, CPF, E-mail, Celular ou Chave aleatória.</param>
/// <param name="Key">A chave como as pessoas a escrevem (CNPJ e CPF com pontuação, celular com +55).</param>
public sealed record InvoicePix(string KindLabel, string Key);

public sealed record InvoiceAccount(string Bank, string Agency, string Number, string KindLabel);

/// <summary>Como pagar: a chave Pix e/ou a conta, o que o condomínio tiver cadastrado. Nunca os dois vazios.</summary>
public sealed record InvoiceHowToPay(InvoicePix? Pix, InvoiceAccount? Account);

/// <summary>
/// Tudo o que o PDF mostra, já decidido: o renderizador só desenha. É a fatura, o pedido de pagamento: sai igual antes e depois
/// de pagar, então nada aqui fala de pagamento feito (isso é do recibo, que ainda não existe).
/// </summary>
/// <param name="Reference">Código curto para o síndico e o morador falarem da mesma fatura.</param>
/// <param name="ResponsibleName">Quem recebe a fatura; nulo quando a unidade não tem responsável.</param>
/// <param name="IssuedOn">Dia do fechamento do mês, em Brasília.</param>
/// <param name="Statement">O demonstrativo dos gastos do condomínio no mês, despesa por despesa (o consumo individual de água, esgoto e gás não entra).</param>
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
    InvoiceShare Share,
    InvoiceStatement Statement,
    InvoiceHowToPay? HowToPay,
    InvoiceManager? Manager);

/// <param name="FileName">Nome seguro para o download (sem pasta, acento ou espaço).</param>
public sealed record InvoiceFile(string FileName, byte[] Content)
{
    public override string ToString() => $"{nameof(InvoiceFile)} {{ {nameof(FileName)} = {FileName}, Bytes = {Content.Length} }}";
}
