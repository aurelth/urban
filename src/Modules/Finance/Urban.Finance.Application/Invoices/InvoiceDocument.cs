using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Months;

namespace Urban.Finance.Application.Invoices;

/// <param name="Quantity">Consumo em m³ (água, esgoto e gás); nulo nas demais linhas.</param>
public sealed record InvoiceLine(ChargeLineKind Kind, string Label, decimal Amount, decimal? Quantity, decimal? PreviousReading, decimal? CurrentReading);

/// <summary>Tudo o que o PDF mostra, já decidido: o renderizador só desenha.</summary>
/// <param name="Reference">Código curto para o síndico e o morador falarem da mesma fatura.</param>
/// <param name="ResponsibleName">Quem recebe a fatura; nulo quando a unidade não tem responsável.</param>
/// <param name="IssuedOn">Dia do fechamento do mês, em Brasília.</param>
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
    decimal Total);

/// <param name="FileName">Nome seguro para o download (sem pasta, acento ou espaço).</param>
public sealed record InvoiceFile(string FileName, byte[] Content)
{
    public override string ToString() => $"{nameof(InvoiceFile)} {{ {nameof(FileName)} = {FileName}, Bytes = {Content.Length} }}";
}
