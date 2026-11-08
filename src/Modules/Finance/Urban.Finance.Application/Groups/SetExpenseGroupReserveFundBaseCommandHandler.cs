using MediatR;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Expenses;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Finance.Application.Groups;

public sealed class SetExpenseGroupReserveFundBaseCommandHandler(IExpenseGroups groups, ITenantContext tenant)
    : IRequestHandler<SetExpenseGroupReserveFundBaseCommand, Result<ExpenseGroupView>>
{
    public async Task<Result<ExpenseGroupView>> Handle(SetExpenseGroupReserveFundBaseCommand request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is null)
        {
            return Result.Failure<ExpenseGroupView>(FinanceErrors.CondominiumNotFound);
        }

        if (string.IsNullOrWhiteSpace(request.Version))
        {
            return Result.Failure<ExpenseGroupView>(FinanceErrors.VersionRequired);
        }

        ExpenseGroupRecord? record = await groups.FindAsync(request.GroupId, cancellationToken);

        if (record is null)
        {
            return Result.Failure<ExpenseGroupView>(ExpenseGroupErrors.NotFound);
        }

        if (request.Version != record.Version)
        {
            return Result.Failure<ExpenseGroupView>(FinanceErrors.ConcurrencyConflict);
        }

        record.Group.SetReserveFundBase(request.Enabled);
        Result<string> saved = await groups.SaveAsync(record, cancellationToken);

        return saved.IsSuccess
            ? Result.Success(ExpenseGroupView.From(record with { Version = saved.Value }))
            : Result.Failure<ExpenseGroupView>(saved.Error);
    }
}
