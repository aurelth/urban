using MediatR;
using Urban.SharedKernel;

namespace Urban.Finance.Application.MyMonth;

/// <summary>
/// Como a conta mora no condomínio hoje (unit, dependent ou none), sem ler mês nem cobrança: é o que o menu pergunta para saber se
/// mostra "Minha fatura" ao síndico. A conta vem do token, nunca da requisição.
/// </summary>
public sealed record GetMyAccessQuery(Guid UserId) : IRequest<Result<MyMonthAccess>>;
