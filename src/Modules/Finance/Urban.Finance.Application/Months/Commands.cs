using MediatR;
using Urban.Finance.Domain.Expenses;
using Urban.SharedKernel;

namespace Urban.Finance.Application.Months;

/// <summary>
/// Abre o mês: gera o lançamento de toda despesa que vence nele e ainda não tem um. Pode ser repetido à vontade; o que já existe
/// (inclusive o que foi editado à mão) não é tocado. O mês vai como texto "AAAA-MM".
/// </summary>
public sealed record OpenMonthCommand(string? Month) : IRequest<Result<MonthView>>;

/// <summary>Os lançamentos do mês como estão, sem gerar nada. Um mês nunca aberto volta vazio.</summary>
public sealed record GetMonthQuery(string? Month) : IRequest<Result<MonthView>>;

/// <summary>Informa ou corrige o valor de um lançamento. Vale só para aquele mês.</summary>
public sealed record SetEntryAmountCommand(Guid EntryId, string? Version, decimal Amount) : IRequest<Result<ExpenseEntryView>>;

/// <summary>
/// Marca o lançamento como pendente, pago ou cancelado. Pago exige o valor informado. "Vencido" não se marca: é a situação de um
/// pendente depois da data de vencimento, calculada a cada leitura.
/// </summary>
public sealed record SetEntryStatusCommand(Guid EntryId, string? Version, EntryStatus Status) : IRequest<Result<ExpenseEntryView>>;
