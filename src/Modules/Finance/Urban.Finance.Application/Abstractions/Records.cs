using Urban.Finance.Domain.Expenses;

namespace Urban.Finance.Application.Abstractions;

/// <summary>O grupo como a leitura do banco o entrega, com a versão da linha (no PostgreSQL, o <c>xmin</c>).</summary>
public sealed record ExpenseGroupRecord(ExpenseGroup Group, string Version);

/// <summary>A despesa com o cronograma (parcelas e reajustes) e a versão da linha da despesa.</summary>
public sealed record ExpenseRecord(Expense Expense, string Version);

public sealed record ExpenseEntryRecord(ExpenseEntry Entry, string Version);
