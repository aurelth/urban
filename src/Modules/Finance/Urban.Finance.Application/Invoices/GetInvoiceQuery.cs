using MediatR;
using Urban.SharedKernel;

namespace Urban.Finance.Application.Invoices;

public sealed record GetInvoiceQuery(Guid ChargeId) : IRequest<Result<InvoiceFile>>;
