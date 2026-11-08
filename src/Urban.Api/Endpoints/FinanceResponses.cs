namespace Urban.Api.Endpoints;

public sealed record ExpenseGroupResponse(Guid Id, string Name, string Version);

public sealed record InstallmentResponse(int Number, string Month, decimal Amount);

public sealed record AmountChangeResponse(string FromMonth, decimal Amount);

/// <summary>Uma despesa como a tela a vê. <c>Kind</c> é "single", "fixed", "variable" ou "installment"; os meses vão como "AAAA-MM".</summary>
/// <param name="CurrentAmount">O valor em vigor no mês atual (o último, se a despesa já acabou; o primeiro, se ainda não começou). Nulo nas variáveis.</param>
public sealed record ExpenseResponse(
    Guid Id,
    Guid GroupId,
    string Description,
    string? Supplier,
    string Kind,
    string StartMonth,
    string? EndMonth,
    string? LastMonth,
    decimal? CurrentAmount,
    int? InstallmentCount,
    int? CurrentInstallment,
    IReadOnlyList<InstallmentResponse> Installments,
    IReadOnlyList<AmountChangeResponse> AmountChanges,
    bool IsEnded,
    string Version);

/// <param name="Amount">Nulo enquanto o lançamento está pendente.</param>
/// <param name="Label">A parcela, como "4/6"; nulo nas outras despesas.</param>
/// <param name="SuggestedAmount">Só nos pendentes: o valor do mês anterior da mesma despesa.</param>
public sealed record ExpenseEntryResponse(
    Guid Id,
    Guid ExpenseId,
    Guid GroupId,
    string Month,
    string Description,
    decimal? Amount,
    string? Label,
    bool IsPending,
    decimal? SuggestedAmount,
    string Version);

public sealed record GroupTotalResponse(Guid GroupId, string GroupName, decimal Total, int PendingCount);

public sealed record MonthResponse(
    string Month,
    IReadOnlyList<ExpenseEntryResponse> Entries,
    IReadOnlyList<GroupTotalResponse> Groups,
    decimal Total,
    int PendingCount);
