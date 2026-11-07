using MediatR;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Expenses;
using Urban.Finance.Domain.Months;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Finance.Application.Expenses;

/// <summary>
/// Cria a despesa e, se o mês inicial já pode ser aberto, o lançamento dele, numa unidade atômica só. Os meses seguintes
/// ganham o lançamento quando o mês é aberto (<c>OpenMonthCommand</c>).
/// </summary>
public sealed class CreateExpenseCommandHandler(
    IExpenses expenses,
    IExpenseGroups groups,
    IExpenseEntries entries,
    ITenantContext tenant,
    IAtomicWork atomicWork,
    IClock clock) : IRequestHandler<CreateExpenseCommand, Result<ExpenseView>>
{
    public async Task<Result<ExpenseView>> Handle(CreateExpenseCommand request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is not { } condominiumId)
        {
            return Result.Failure<ExpenseView>(FinanceErrors.CondominiumNotFound);
        }

        Result<YearMonth> start = YearMonth.Parse(request.StartMonth);

        if (start.IsFailure)
        {
            return Result.Failure<ExpenseView>(start.Error);
        }

        Result<YearMonth?> end = ParseOptional(request.EndMonth);

        if (end.IsFailure)
        {
            return Result.Failure<ExpenseView>(end.Error);
        }

        if (await groups.FindAsync(request.GroupId, cancellationToken) is null)
        {
            return Result.Failure<ExpenseView>(ExpenseGroupErrors.NotFound);
        }

        DateTimeOffset now = clock.UtcNow;
        Result<Expense> created = Build(request, condominiumId, start.Value, end.Value, now);

        if (created.IsFailure)
        {
            return Result.Failure<ExpenseView>(created.Error);
        }

        Expense expense = created.Value;

        return await Atomic.RunAsync(atomicWork, ct => StoreAsync(expense, now, ct), cancellationToken);
    }

    private async Task<Result<ExpenseView>> StoreAsync(Expense expense, DateTimeOffset now, CancellationToken cancellationToken)
    {
        Result<string> added = await expenses.AddAsync(expense, cancellationToken);

        if (added.IsFailure)
        {
            return Result.Failure<ExpenseView>(added.Error);
        }

        if (MonthRules.CanOpen(expense.StartMonth, FinanceCalendar.Today(now)).IsSuccess)
        {
            Result stored = await entries.AddRangeAsync([expense.GenerateEntry(expense.StartMonth, now).Value], cancellationToken);

            if (stored.IsFailure)
            {
                return Result.Failure<ExpenseView>(stored.Error);
            }
        }

        return Result.Success(ExpenseView.From(expense, added.Value, FinanceCalendar.CurrentMonth(now)));
    }

    private static Result<Expense> Build(CreateExpenseCommand request, Guid condominiumId, YearMonth start, YearMonth? end, DateTimeOffset now)
    {
        switch (request.Kind)
        {
            case ExpenseKind.Single:
                return request.Amount is { } single
                    ? Expense.CreateSingle(condominiumId, request.GroupId, request.Description, request.Supplier, start, single, now)
                    : Result.Failure<Expense>(ExpenseErrors.AmountInvalid);
            case ExpenseKind.Fixed:
                return request.Amount is { } monthly
                    ? Expense.CreateFixed(condominiumId, request.GroupId, request.Description, request.Supplier, start, end, monthly, now)
                    : Result.Failure<Expense>(ExpenseErrors.AmountInvalid);
            case ExpenseKind.Variable:
                return Expense.CreateVariable(condominiumId, request.GroupId, request.Description, request.Supplier, start, end, now);
            case ExpenseKind.Installment:
                if (request.Amount is not { } total)
                {
                    return Result.Failure<Expense>(ExpenseErrors.AmountInvalid);
                }

                return request.InstallmentCount is { } count
                    ? Expense.CreateInstallment(condominiumId, request.GroupId, request.Description, request.Supplier, total, count, start, request.CurrentInstallment ?? 1, now)
                    : Result.Failure<Expense>(ExpenseErrors.InstallmentCountInvalid);
            default:
                return Result.Failure<Expense>(ExpenseErrors.KindInvalid);
        }
    }

    private static Result<YearMonth?> ParseOptional(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Result.Success<YearMonth?>(null);
        }

        Result<YearMonth> parsed = YearMonth.Parse(text);

        return parsed.IsSuccess ? Result.Success<YearMonth?>(parsed.Value) : Result.Failure<YearMonth?>(parsed.Error);
    }
}
