using MediatR;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Payments;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;

namespace Urban.Finance.Application.Invoices;

public sealed class GetInvoiceQueryHandler(ICharges charges, IInvoiceParties parties, IInvoiceRenderer renderer, ITenantContext tenant, IClock clock)
    : IRequestHandler<GetInvoiceQuery, Result<InvoiceFile>>
{
    public async Task<Result<InvoiceFile>> Handle(GetInvoiceQuery request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is null)
        {
            return Result.Failure<InvoiceFile>(FinanceErrors.CondominiumNotFound);
        }

        if (await charges.FindAsync(request.ChargeId, cancellationToken) is not { } detail)
        {
            return Result.Failure<InvoiceFile>(PaymentErrors.ChargeNotFound);
        }

        if (await parties.GetCondominiumAsync(cancellationToken) is not { } condominium)
        {
            return Result.Failure<InvoiceFile>(FinanceErrors.CondominiumNotFound);
        }

        UnitCharge charge = detail.Charge;
        DateOnly today = FinanceCalendar.Today(clock.UtcNow);
        IReadOnlyDictionary<Guid, InvoiceResponsible> responsibles = await parties.GetResponsiblesAsync([charge.UnitId], today, cancellationToken);

        InvoiceDocument document = new(
            InvoiceText.Reference(detail.Month, charge.Id),
            condominium.Name,
            condominium.Cnpj,
            condominium.Address,
            InvoiceText.Unit(charge.Block, charge.Number),
            responsibles.TryGetValue(charge.UnitId, out InvoiceResponsible? responsible) ? responsible.Name : null,
            detail.Month,
            detail.DueDate,
            FinanceCalendar.Today(detail.ClosedAt),
            [.. charge.Lines.OrderBy(line => line.Position).Select(line => new InvoiceLine(line.Kind, line.Label, line.Amount, line.Quantity, line.PreviousReading, line.CurrentReading))],
            charge.Amount);

        return Result.Success(new InvoiceFile(InvoiceText.FileName(detail.Month, charge.Block, charge.Number), renderer.Render(document)));
    }
}
