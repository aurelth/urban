using MediatR;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Expenses;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Finance.Application.Groups;

public sealed class RenameExpenseGroupCommandHandler(IExpenseGroups groups, ITenantContext tenant)
    : IRequestHandler<RenameExpenseGroupCommand, Result<ExpenseGroupView>>
{
    public async Task<Result<ExpenseGroupView>> Handle(RenameExpenseGroupCommand request, CancellationToken cancellationToken)
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

        Result<string> name = ExpenseGroup.ValidateName(request.Name);

        if (name.IsFailure)
        {
            return Result.Failure<ExpenseGroupView>(name.Error);
        }

        if (await groups.IsNameTakenAsync(name.Value, record.Group.Id, cancellationToken))
        {
            return Result.Failure<ExpenseGroupView>(ExpenseGroupErrors.NameAlreadyExists);
        }

        record.Group.Rename(name.Value);
        Result<string> saved = await groups.SaveAsync(record, cancellationToken);

        return saved.IsSuccess
            ? Result.Success(ExpenseGroupView.From(record with { Version = saved.Value }))
            : Result.Failure<ExpenseGroupView>(saved.Error);
    }
}
