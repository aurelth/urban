using MediatR;
using Urban.Finance.Domain.Expenses;
using Urban.SharedKernel;

namespace Urban.Finance.Application.Expenses;

public sealed record ListExpensesQuery(ExpenseStatusFilter Status = ExpenseStatusFilter.Active) : IRequest<Result<IReadOnlyList<ExpenseView>>>;

/// <summary>
/// Cria a despesa em uma das quatro formas. Os meses vão como texto "AAAA-MM". <paramref name="Amount"/> é o valor da única e da
/// fixa, e o total da parcelada (a variável não tem valor). <paramref name="CurrentInstallment"/> é a parcela que cai em
/// <paramref name="StartMonth"/>; sem ela, começa na primeira.
/// </summary>
public sealed record CreateExpenseCommand(
    ExpenseKind Kind,
    Guid GroupId,
    string? Description,
    string? Supplier,
    string? StartMonth,
    string? EndMonth,
    decimal? Amount,
    int? InstallmentCount,
    int? CurrentInstallment) : IRequest<Result<ExpenseView>>;

public sealed record UpdateExpenseCommand(Guid ExpenseId, string? Version, string? Description, string? Supplier, Guid GroupId) : IRequest<Result<ExpenseView>>;

/// <summary>Muda o valor de uma despesa fixa a partir de um mês. Os meses anteriores continuam como estavam.</summary>
public sealed record ChangeExpenseAmountCommand(Guid ExpenseId, string? Version, string? FromMonth, decimal Amount) : IRequest<Result<ExpenseView>>;

public sealed record SetInstallmentAmountCommand(Guid ExpenseId, string? Version, int Number, decimal Amount) : IRequest<Result<ExpenseView>>;

public sealed record EndExpenseCommand(Guid ExpenseId, string? Version, string? LastMonth) : IRequest<Result<ExpenseView>>;
