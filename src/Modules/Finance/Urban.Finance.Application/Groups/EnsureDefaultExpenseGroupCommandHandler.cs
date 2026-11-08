using MediatR;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Expenses;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Finance.Application.Groups;

/// <summary>
/// A tela chama este comando na primeira visita, e não existe carga inicial de dados: o grupo padrão nasce quando é preciso.
/// Se duas requisições chegam juntas, o índice único do banco deixa só uma criar e a outra devolve a que ganhou.
/// </summary>
public sealed class EnsureDefaultExpenseGroupCommandHandler(IExpenseGroups groups, ITenantContext tenant, IClock clock)
    : IRequestHandler<EnsureDefaultExpenseGroupCommand, Result<ExpenseGroupView>>
{
    public async Task<Result<ExpenseGroupView>> Handle(EnsureDefaultExpenseGroupCommand request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is not { } condominiumId)
        {
            return Result.Failure<ExpenseGroupView>(FinanceErrors.CondominiumNotFound);
        }

        if (await FindDefaultAsync(cancellationToken) is { } existing)
        {
            return Result.Success(ExpenseGroupView.From(existing));
        }

        ExpenseGroup group = ExpenseGroup.Create(condominiumId, ExpenseGroup.DefaultName, clock.UtcNow, reserveFundBase: true).Value;
        Result<string> added = await groups.AddAsync(group, cancellationToken);

        if (added.IsSuccess)
        {
            return Result.Success(ExpenseGroupView.From(new ExpenseGroupRecord(group, added.Value)));
        }

        ExpenseGroupRecord? winner = added.Error == ExpenseGroupErrors.NameAlreadyExists ? await FindDefaultAsync(cancellationToken) : null;

        return winner is null
            ? Result.Failure<ExpenseGroupView>(added.Error)
            : Result.Success(ExpenseGroupView.From(winner));
    }

    private async Task<ExpenseGroupRecord?> FindDefaultAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<ExpenseGroupRecord> all = await groups.ListAsync(cancellationToken);

        return all.FirstOrDefault(record => string.Equals(record.Group.Name, ExpenseGroup.DefaultName, StringComparison.OrdinalIgnoreCase));
    }
}
