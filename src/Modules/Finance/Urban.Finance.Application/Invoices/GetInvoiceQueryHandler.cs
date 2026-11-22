using MediatR;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Expenses;
using Urban.Finance.Domain.Payments;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;

namespace Urban.Finance.Application.Invoices;

public sealed class GetInvoiceQueryHandler(
    ICharges charges,
    IExpenseEntries entries,
    IExpenseGroups groups,
    IPaymentInstructionsStore instructions,
    IInvoiceParties parties,
    IResidentHousing housing,
    IInvoiceRenderer renderer,
    ITenantContext tenant,
    IClock clock)
    : IRequestHandler<GetInvoiceQuery, Result<InvoiceFile>>
{
    private const string FallbackGroupName = "Despesas";

    public async Task<Result<InvoiceFile>> Handle(GetInvoiceQuery request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is null)
        {
            return Result.Failure<InvoiceFile>(FinanceErrors.CondominiumNotFound);
        }

        _ = housing;

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
        InvoiceStatement statement = await StatementOfAsync(detail, cancellationToken);
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
            new InvoiceShare(detail.BasisKind, ShareOf(charge, detail)),
            statement,
            HowToPayOf(howToPay),
            condominium.Manager);

        return Result.Success(new InvoiceFile(InvoiceText.FileName(detail.Month, charge.Block, charge.Number), renderer.Render(document)));
    }

    private static decimal ShareOf(UnitCharge charge, ChargeDetail detail) =>
        detail.TotalWeight > 0 ? Math.Round(charge.Weight / detail.TotalWeight * 100m, 6, MidpointRounding.AwayFromZero) : 0m;

    /// <summary>
    /// O que o condomínio gastou no mês, despesa por despesa. Mês fechado não aceita mudança de lançamento, então ler os lançamentos
    /// dá o mesmo que no fechamento. Água, esgoto e gás são consumo de cada unidade e ficam só na cobrança dela.
    /// </summary>
    private async Task<InvoiceStatement> StatementOfAsync(ChargeDetail detail, CancellationToken cancellationToken)
    {
        IReadOnlyList<ExpenseEntryRecord> records = await entries.ListAsync(detail.Month, cancellationToken);
        Dictionary<Guid, string> names = (await groups.ListAsync(cancellationToken)).ToDictionary(record => record.Group.Id, record => record.Group.Name);

        List<InvoiceStatementGroup> statementGroups =
        [
            .. records
                .Select(record => record.Entry)
                .Where(entry => entry.Status != EntryStatus.Canceled && entry.Amount is > 0)
                .GroupBy(entry => entry.GroupId)
                .Select(group => new InvoiceStatementGroup(
                    names.GetValueOrDefault(group.Key, FallbackGroupName),
                    [.. group
                        .OrderBy(entry => entry.Description, AlphabeticalOrder.Comparer)
                        .ThenBy(entry => entry.Label, StringComparer.Ordinal)
                        .Select(entry => new InvoiceStatementItem(InvoiceText.ExpenseItem(entry.Description, entry.Label), entry.Amount!.Value, InvoiceText.ExpenseDetail(entry.Supplier, entry.Note)))]))
                .OrderBy(group => group.Name, AlphabeticalOrder.Comparer),
        ];

        decimal reserveFund = detail.LineTotals.Where(total => total.Kind == ChargeLineKind.ReserveFund).Sum(total => total.Amount);

        return new InvoiceStatement(statementGroups, reserveFund > 0 ? new InvoiceReserveFund(reserveFund, detail.ReserveFundPercent, detail.ReserveFundBase) : null);
    }

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
