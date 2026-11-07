using MediatR;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Expenses;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Finance.Application.Groups;

public sealed class CreateExpenseGroupCommandHandler(IExpenseGroups groups, ITenantContext tenant, IClock clock)
    : IRequestHandler<CreateExpenseGroupCommand, Result<ExpenseGroupView>>
{
    public async Task<Result<ExpenseGroupView>> Handle(CreateExpenseGroupCommand request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is not { } condominiumId)
        {
            return Result.Failure<ExpenseGroupView>(FinanceErrors.CondominiumNotFound);
        }

        Result<ExpenseGroup> created = ExpenseGroup.Create(condominiumId, request.Name, clock.UtcNow);

        if (created.IsFailure)
        {
            return Result.Failure<ExpenseGroupView>(created.Error);
        }

        ExpenseGroup group = created.Value;

        if (await groups.IsNameTakenAsync(group.Name, null, cancellationToken))
        {
            return Result.Failure<ExpenseGroupView>(ExpenseGroupErrors.NameAlreadyExists);
        }

        Result<string> added = await groups.AddAsync(group, cancellationToken);

        return added.IsSuccess
            ? Result.Success(ExpenseGroupView.From(new ExpenseGroupRecord(group, added.Value)))
            : Result.Failure<ExpenseGroupView>(added.Error);
    }
}
