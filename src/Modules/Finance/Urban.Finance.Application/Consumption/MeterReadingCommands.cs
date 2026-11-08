using MediatR;
using Urban.Finance.Domain.Consumption;
using Urban.SharedKernel;

namespace Urban.Finance.Application.Consumption;

/// <summary>As leituras do mês de cada unidade do condomínio, com a leitura do mês anterior para sugerir como ponto de partida. O mês vai como texto "AAAA-MM".</summary>
public sealed record GetMonthReadingsQuery(string? Month) : IRequest<Result<MonthReadingsView>>;

/// <summary>
/// Informa ou corrige leituras do mês. Sem a leitura anterior, vale a leitura atual do mês passado (ou zero na primeira vez).
/// Sem a leitura atual, a leitura da unidade e do serviço é retirada. O pedido é aceito inteiro ou recusado inteiro.
/// </summary>
public sealed record SaveMonthReadingsCommand(string? Month, IReadOnlyList<ReadingInput>? Readings) : IRequest<Result<MonthReadingsView>>;

public sealed record ReadingInput(Guid UnitId, ConsumptionService Service, decimal? Previous, decimal? Current);
