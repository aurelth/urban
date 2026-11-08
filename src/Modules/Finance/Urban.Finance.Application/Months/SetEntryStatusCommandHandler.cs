using MediatR;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Expenses;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Finance.Application.Months;

public sealed class SetEntryStatusCommandHandler(IExpenseEntries entries, IMonthClosings closings, ITenantContext tenant, IClock clock)
    : IRequestHandler<SetEntryStatusCommand, Result<ExpenseEntryView>>
{
    public async Task<Result<ExpenseEntryView>> Handle(SetEntryStatusCommand request, CancellationToken cancellationToken)
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

        // Pagar não mexe nas cobranças dos moradores; cancelar (ou desfazer o cancelamento) muda o total que foi repartido.
        bool changesTheTotal = (request.Status == EntryStatus.Canceled) != (record.Entry.Status == EntryStatus.Canceled);

        if (changesTheTotal && await closings.FindAsync(record.Entry.Month, cancellationToken) is not null)
        {
            return Result.Failure<ExpenseEntryView>(ClosingErrors.MonthIsClosed);
        }

        Result changed = record.Entry.SetStatus(request.Status);

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
