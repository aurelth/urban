using MediatR;
using Urban.SharedKernel;

namespace Urban.Finance.Application.Invoices;

/// <param name="ChargeId">A cobrança.</param>
/// <param name="ResidentUserId">
/// Quando vem preenchido, é um morador pedindo a própria fatura: só vale se a cobrança for de uma unidade em que essa conta mora
/// hoje (senão responde como se a cobrança não existisse). O síndico pede sem ele.
/// </param>
public sealed record GetInvoiceQuery(Guid ChargeId, Guid? ResidentUserId = null) : IRequest<Result<InvoiceFile>>;
