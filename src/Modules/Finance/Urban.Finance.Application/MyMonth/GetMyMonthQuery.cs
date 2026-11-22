using MediatR;
using Urban.SharedKernel;

namespace Urban.Finance.Application.MyMonth;

/// <summary>
/// O mês financeiro de quem mora no condomínio: a cobrança de cada unidade dele e as despesas do mês por grupo. A conta vem do
/// token, nunca da requisição.
/// </summary>
public sealed record GetMyMonthQuery(Guid UserId, string Month) : IRequest<Result<MyMonthView>>;
