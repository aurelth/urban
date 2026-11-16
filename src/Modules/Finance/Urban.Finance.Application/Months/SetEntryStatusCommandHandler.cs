using MediatR;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Expenses;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Finance.Application.Months;

public sealed class SetEntryStatusCommandHandler(IExpenseEntries entries, IMonthClosings closings, ITenantContext tenant, IClock clock)
    : IRequestHandler<SetEntryStatusCommand, Result<ExpenseEntryView>>
{
    public Task<Result<ExpenseEntryView>> Handle(SetEntryStatusCommand request, CancellationToken cancellationToken) =>
        EntryChange.ApplyAsync(entries, tenant, clock, request.EntryId, request.Version, record => ChangeAsync(record, request.Status, cancellationToken), cancellationToken);

    private async Task<Result> ChangeAsync(ExpenseEntryRecord record, EntryStatus status, CancellationToken cancellationToken)
    {
        // Pagar não mexe nas cobranças dos moradores; cancelar (ou desfazer o cancelamento) muda o total que foi repartido.
        bool changesTheTotal = (status == EntryStatus.Canceled) != (record.Entry.Status == EntryStatus.Canceled);

        return changesTheTotal
            ? await EntryChange.WhileMonthIsOpenAsync(closings, record.Entry, entry => entry.SetStatus(status), cancellationToken)
            : record.Entry.SetStatus(status);
    }
}
