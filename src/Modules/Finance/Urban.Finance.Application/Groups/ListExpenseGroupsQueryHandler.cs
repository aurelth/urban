using MediatR;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Finance.Application.Groups;

public sealed class ListExpenseGroupsQueryHandler(IExpenseGroups groups, ITenantContext tenant)
    : IRequestHandler<ListExpenseGroupsQuery, Result<IReadOnlyList<ExpenseGroupView>>>
{
    public async Task<Result<IReadOnlyList<ExpenseGroupView>>> Handle(ListExpenseGroupsQuery request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is null)
        {
            return Result.Failure<IReadOnlyList<ExpenseGroupView>>(FinanceErrors.CondominiumNotFound);
        }

        IReadOnlyList<ExpenseGroupRecord> records = await groups.ListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<ExpenseGroupView>>([.. records.Select(ExpenseGroupView.From)]);
    }
}
