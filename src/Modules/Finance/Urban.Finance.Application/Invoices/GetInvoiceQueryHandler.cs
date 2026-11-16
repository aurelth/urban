using MediatR;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Payments;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;

namespace Urban.Finance.Application.Invoices;

public sealed class GetInvoiceQueryHandler(
    ICharges charges,
    IPayments payments,
    IPaymentInstructionsStore instructions,
    IInvoiceParties parties,
    IInvoiceRenderer renderer,
    ITenantContext tenant,
    IClock clock)
    : IRequestHandler<GetInvoiceQuery, Result<InvoiceFile>>
{
    /// <summary>Quantas cobranças antigas a fatura lista, no máximo; a unidade que deve mais que três anos tem um problema maior que o PDF.</summary>
    public const int EarlierChargesLimit = 36;

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
        decimal paid = ChargeAccount.Paid(await payments.ListForChargeAsync(charge.Id, cancellationToken));
        IReadOnlyList<EarlierCharge> earlier = await charges.ListEarlierOpenAsync(charge.UnitId, detail.Month, EarlierChargesLimit, cancellationToken);
        PaymentInstructions? howToPay = await instructions.FindAsync(cancellationToken);

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
            charge.Amount,
            new InvoiceSituation(ChargeAccount.StatusOf(charge.Amount, paid, detail.DueDate, today), paid, ChargeAccount.Balance(charge.Amount, paid)),
            new InvoiceShare(detail.BasisKind, ShareOf(charge, detail)),
            StatementOf(detail),
            [.. earlier.Select(item => new InvoiceEarlierCharge(item.Month, item.DueDate, item.Amount, item.Paid))],
            HowToPayOf(howToPay),
            condominium.Manager);

        return Result.Success(new InvoiceFile(InvoiceText.FileName(detail.Month, charge.Block, charge.Number), renderer.Render(document)));
    }

    private static decimal ShareOf(UnitCharge charge, ChargeDetail detail) =>
        detail.TotalWeight > 0 ? Math.Round(charge.Weight / detail.TotalWeight * 100m, 6, MidpointRounding.AwayFromZero) : 0m;

    /// <summary>O que o condomínio gastou no mês: os grupos e o fundo de reserva. Água, esgoto e gás são consumo de cada unidade e ficam só no recibo dela.</summary>
    private static List<InvoiceStatementLine> StatementOf(ChargeDetail detail) =>
        [.. detail.LineTotals
            .Where(total => total.Kind is ChargeLineKind.Expenses or ChargeLineKind.ReserveFund)
            .Select(total => new InvoiceStatementLine(total.Label, total.Amount))];

    private static InvoiceHowToPay? HowToPayOf(PaymentInstructions? stored)
    {
        if (stored is null || (stored.Pix is null && stored.Account is null))
        {
            return null;
        }

        InvoicePix? pix = stored.Pix is { } key ? new InvoicePix(InvoiceText.PixKind(key.Kind), InvoiceText.PixKey(key.Kind, key.Key)) : null;
        InvoiceAccount? account = stored.Account is { } bank ? new InvoiceAccount(bank.Bank, bank.Agency, bank.Number, InvoiceText.AccountKind(bank.Kind)) : null;

        return new InvoiceHowToPay(pix, account);
    }
}
