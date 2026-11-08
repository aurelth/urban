namespace Urban.Api.Endpoints;

/// <param name="Version">Só na troca de nome: a versão lida na lista de grupos.</param>
public sealed record ExpenseGroupRequest(string? Version, string? Name);

/// <summary>
/// Os dados de uma despesa nova. <c>Kind</c> é "single", "fixed", "variable" ou "installment"; os meses vão como "AAAA-MM".
/// <c>Amount</c> é o valor da única e da fixa e o total da parcelada; a variável não tem valor. <c>CurrentInstallment</c> é a parcela
/// que cai em <c>StartMonth</c> (a primeira, se faltar). <c>DueDay</c> é o dia do vencimento, de 1 a 31, e é obrigatório.
/// </summary>
public sealed record ExpenseRequest(
    string? Kind,
    Guid? GroupId,
    string? Description,
    string? Supplier,
    string? StartMonth,
    string? EndMonth,
    decimal? Amount,
    int? InstallmentCount,
    int? CurrentInstallment,
    int? DueDay);

public sealed record ExpenseUpdateRequest(string? Version, string? Description, string? Supplier, Guid? GroupId, int? DueDay);

/// <param name="FromMonth">O mês ("AAAA-MM") a partir do qual o novo valor vale.</param>
public sealed record ExpenseAmountRequest(string? Version, string? FromMonth, decimal? Amount);

public sealed record InstallmentAmountRequest(string? Version, decimal? Amount);

/// <param name="LastMonth">O último mês ("AAAA-MM") em que a despesa ainda é cobrada.</param>
public sealed record EndExpenseRequest(string? Version, string? LastMonth);

public sealed record EntryAmountRequest(string? Version, decimal? Amount);

/// <param name="Status">"pending", "paid" ou "canceled". "Vencido" não se envia: é calculado pela data.</param>
public sealed record EntryStatusRequest(string? Version, string? Status);
