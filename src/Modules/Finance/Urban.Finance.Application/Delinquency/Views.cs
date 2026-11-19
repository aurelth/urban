namespace Urban.Finance.Application.Delinquency;

/// <summary>Uma cobrança em atraso de uma unidade.</summary>
public sealed record OverdueChargeView(Guid ChargeId, string Month, DateOnly DueDate, decimal Amount, decimal Paid, decimal Balance, int DaysOverdue);

/// <summary>Uma unidade devedora: quanto deve no total, desde quando e quais cobranças (da mais antiga para a mais nova).</summary>
public sealed record DebtorView(
    Guid UnitId, string? Block, string Number, decimal Balance, int OverdueCharges, DateOnly OldestDueDate, int DaysOverdue, IReadOnlyList<OverdueChargeView> Charges);

/// <summary><paramref name="TotalOverdue"/> e <paramref name="Units"/> valem para todas as unidades devedoras, não só para a página.</summary>
public sealed record DelinquencyView(DateOnly AsOf, decimal TotalOverdue, int Units, int Page, int PageSize, IReadOnlyList<DebtorView> Items);
