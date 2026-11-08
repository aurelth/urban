namespace Urban.Api.Endpoints;

public sealed record ExpenseGroupResponse(Guid Id, string Name, bool ReserveFundBase, string Version);

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
    int DueDay,
    int? InstallmentCount,
    int? CurrentInstallment,
    IReadOnlyList<InstallmentResponse> Installments,
    IReadOnlyList<AmountChangeResponse> AmountChanges,
    bool IsEnded,
    string Version);

/// <param name="Amount">Nulo enquanto o valor de uma despesa variável não foi informado.</param>
/// <param name="Label">A parcela, como "4/6"; nulo nas outras despesas.</param>
/// <param name="DueDate">O vencimento, como "AAAA-MM-DD".</param>
/// <param name="Status">"pending", "paid", "overdue" (pendente depois do vencimento) ou "canceled".</param>
/// <param name="NeedsAmount">Verdadeiro enquanto o valor ainda não foi informado.</param>
/// <param name="SuggestedAmount">Só nos que precisam de valor: o valor do mês anterior da mesma despesa.</param>
public sealed record ExpenseEntryResponse(
    Guid Id,
    Guid ExpenseId,
    Guid GroupId,
    string Month,
    string Description,
    decimal? Amount,
    string? Label,
    string DueDate,
    string Status,
    bool NeedsAmount,
    decimal? SuggestedAmount,
    string Version);

public sealed record GroupTotalResponse(Guid GroupId, string GroupName, decimal Total, int NeedsAmountCount);

public sealed record MonthResponse(
    string Month,
    IReadOnlyList<ExpenseEntryResponse> Entries,
    IReadOnlyList<GroupTotalResponse> Groups,
    decimal Total,
    int NeedsAmountCount,
    int OverdueCount,
    MonthClosingResponse? Closing);

/// <param name="Weight">O peso da unidade no rateio (fração ideal de 0 a 1, área em m² ou 1), como era quando o mês fechou.</param>
public sealed record UnitChargeResponse(Guid UnitId, string? Block, string Number, decimal Weight, decimal Amount);

/// <param name="Basis">Em que o rateio se baseou: idealFraction, equalShare ou area.</param>
/// <param name="DueDate">O vencimento das cobranças, AAAA-MM-DD.</param>
/// <param name="Total">Soma dos lançamentos não cancelados quando o mês fechou.</param>
public sealed record MonthClosingResponse(DateTimeOffset ClosedAt, string DueDate, decimal Total, string Basis, IReadOnlyList<UnitChargeResponse> Charges);

public sealed record FinanceSettingsResponse(decimal ReserveFundPercent);

public sealed record TariffBandResponse(decimal? UpToCubicMeters, decimal FixedAmount, decimal PricePerCubicMeter);

public sealed record GasTariffResponse(decimal KilosPerCubicMeter, decimal PricePerKilo, decimal PricePerCubicMeter);

public sealed record ConsumptionTariffResponse(
    string ValidFrom,
    IReadOnlyList<TariffBandResponse> Water,
    IReadOnlyList<TariffBandResponse>? Sewage,
    decimal SewagePercent,
    GasTariffResponse? Gas);
