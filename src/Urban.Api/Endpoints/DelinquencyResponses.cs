namespace Urban.Api.Endpoints;

/// <param name="Month">AAAA-MM.</param>
/// <param name="DueDate">AAAA-MM-DD.</param>
/// <param name="Balance">O que falta pagar desta cobrança.</param>
/// <param name="DaysOverdue">Dias desde o vencimento (1 no dia seguinte a ele).</param>
public sealed record OverdueChargeResponse(Guid ChargeId, string Month, string DueDate, decimal Amount, decimal Paid, decimal Balance, int DaysOverdue);

/// <param name="Balance">Quanto a unidade deve no total, somando as cobranças em atraso.</param>
/// <param name="OldestDueDate">O vencimento mais antigo ainda em aberto (AAAA-MM-DD).</param>
/// <param name="DaysOverdue">Dias de atraso da cobrança mais antiga.</param>
/// <param name="Charges">As cobranças em atraso, da mais antiga para a mais nova.</param>
public sealed record DebtorResponse(
    Guid UnitId, string? Block, string Number, decimal Balance, int OverdueCharges, string OldestDueDate, int DaysOverdue, IReadOnlyList<OverdueChargeResponse> Charges);

/// <param name="AsOf">O dia, em Brasília, a que o cálculo se refere (AAAA-MM-DD).</param>
/// <param name="TotalOverdue">O total em atraso de todas as unidades devedoras, não só da página.</param>
/// <param name="Units">Quantas unidades devem, para a paginação.</param>
public sealed record DelinquencyResponse(string AsOf, decimal TotalOverdue, int Units, int Page, int PageSize, IReadOnlyList<DebtorResponse> Items);
