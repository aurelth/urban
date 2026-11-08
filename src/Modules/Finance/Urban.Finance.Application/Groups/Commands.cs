using MediatR;
using Urban.SharedKernel;

namespace Urban.Finance.Application.Groups;

public sealed record ListExpenseGroupsQuery : IRequest<Result<IReadOnlyList<ExpenseGroupView>>>;

/// <summary>Garante o grupo "Despesas ordinárias" do condomínio e o devolve. Pode ser chamado quantas vezes quiser.</summary>
public sealed record EnsureDefaultExpenseGroupCommand : IRequest<Result<ExpenseGroupView>>;

public sealed record CreateExpenseGroupCommand(string? Name) : IRequest<Result<ExpenseGroupView>>;

public sealed record RenameExpenseGroupCommand(Guid GroupId, string? Version, string? Name) : IRequest<Result<ExpenseGroupView>>;

/// <summary>Marca ou desmarca o grupo como base de cálculo do fundo de reserva.</summary>
public sealed record SetExpenseGroupReserveFundBaseCommand(Guid GroupId, string? Version, bool Enabled) : IRequest<Result<ExpenseGroupView>>;
