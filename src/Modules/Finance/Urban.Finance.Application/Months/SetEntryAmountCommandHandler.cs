using MediatR;
using Urban.Finance.Application.Abstractions;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Finance.Application.Months;

public sealed class SetEntryAmountCommandHandler(IExpenseEntries entries, IMonthClosings closings, ITenantContext tenant, IClock clock)
    : IRequestHandler<SetEntryAmountCommand, Result<ExpenseEntryView>>
{
    public Task<Result<ExpenseEntryView>> Handle(SetEntryAmountCommand request, CancellationToken cancellationToken) =>
        EntryChange.ApplyAsync(
            entries,
            tenant,
            clock,
            request.EntryId,
            request.Version,
            record => EntryChange.WhileMonthIsOpenAsync(closings, record.Entry, entry => entry.SetAmount(request.Amount), cancellationToken),
            cancellationToken);
}
