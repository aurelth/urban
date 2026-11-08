using MediatR;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Expenses;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Finance.Application.Months;

public sealed class SetEntryAmountCommandHandler(IExpenseEntries entries, ITenantContext tenant, IClock clock)
    : IRequestHandler<SetEntryAmountCommand, Result<ExpenseEntryView>>
{
    public async Task<Result<ExpenseEntryView>> Handle(SetEntryAmountCommand request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is null)
        {
            return Result.Failure<ExpenseEntryView>(FinanceErrors.CondominiumNotFound);
        }

        if (string.IsNullOrWhiteSpace(request.Version))
        {
            return Result.Failure<ExpenseEntryView>(FinanceErrors.VersionRequired);
        }

        ExpenseEntryRecord? record = await entries.FindAsync(request.EntryId, cancellationToken);

        if (record is null)
        {
            return Result.Failure<ExpenseEntryView>(ExpenseErrors.EntryNotFound);
        }

        if (request.Version != record.Version)
        {
            return Result.Failure<ExpenseEntryView>(FinanceErrors.ConcurrencyConflict);
        }

        Result changed = record.Entry.SetAmount(request.Amount);

        if (changed.IsFailure)
        {
            return Result.Failure<ExpenseEntryView>(changed.Error);
        }

        Result<string> saved = await entries.SaveAsync(record, cancellationToken);

        return saved.IsSuccess
            ? Result.Success(ExpenseEntryView.From(record with { Version = saved.Value }, null, FinanceCalendar.Today(clock.UtcNow)))
            : Result.Failure<ExpenseEntryView>(saved.Error);
    }
}
