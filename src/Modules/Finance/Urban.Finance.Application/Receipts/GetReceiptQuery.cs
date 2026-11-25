using MediatR;
using Urban.SharedKernel;

namespace Urban.Finance.Application.Receipts;

/// <param name="PaymentId">O pagamento de que se quer o recibo.</param>
/// <param name="ResidentUserId">
/// Quando vem preenchido, é um morador pedindo o recibo de um pagamento da própria unidade: só vale se a cobrança for de uma
/// unidade em que essa conta mora hoje (senão responde como se o pagamento não existisse). O síndico pede sem ele.
/// </param>
public sealed record GetReceiptQuery(Guid PaymentId, Guid? ResidentUserId = null) : IRequest<Result<ReceiptFile>>;
