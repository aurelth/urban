using MediatR;
using Urban.Finance.Application.Abstractions;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Finance.Application.Months;

public sealed class SetEntryNoteCommandHandler(IExpenseEntries entries, IMonthClosings closings, ITenantContext tenant, IClock clock)
    : IRequestHandler<SetEntryNoteCommand, Result<ExpenseEntryView>>
{
    public Task<Result<ExpenseEntryView>> Handle(SetEntryNoteCommand request, CancellationToken cancellationToken) =>
        EntryChange.ApplyAsync(
            entries,
            tenant,
            clock,
            request.EntryId,
            request.Version,
            record => EntryChange.WhileMonthIsOpenAsync(closings, record.Entry, entry => entry.SetNote(request.Note), cancellationToken),
            cancellationToken);
}
