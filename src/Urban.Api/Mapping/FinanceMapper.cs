using System.Globalization;
using Riok.Mapperly.Abstractions;
using Urban.Api.Endpoints;
using Urban.Finance.Application.Expenses;
using Urban.Finance.Application.Groups;
using Urban.Finance.Application.Months;
using Urban.Finance.Domain.Expenses;
using Urban.SharedKernel.Contracts;

namespace Urban.Api.Mapping;

[Mapper]
public static partial class FinanceMapper
{
    // ---- Do corpo da requisição para o comando ----

    public static CreateExpenseCommand ToCommand(ExpenseRequest request) =>
        new(
            ParseKind(request.Kind),
            request.GroupId ?? Guid.Empty,
            request.Description,
            request.Supplier,
            request.StartMonth,
            request.EndMonth,
            request.Amount,
            request.InstallmentCount,
            request.CurrentInstallment,
            request.DueDay);

    public static UpdateExpenseCommand ToCommand(ExpenseUpdateRequest request, Guid expenseId) =>
        new(expenseId, request.Version, request.Description, request.Supplier, request.GroupId ?? Guid.Empty, request.DueDay);

    public static ChangeExpenseAmountCommand ToCommand(ExpenseAmountRequest request, Guid expenseId) =>
        new(expenseId, request.Version, request.FromMonth, request.Amount ?? 0m);

    public static SetInstallmentAmountCommand ToCommand(InstallmentAmountRequest request, Guid expenseId, int number) =>
        new(expenseId, request.Version, number, request.Amount ?? 0m);

    public static EndExpenseCommand ToCommand(EndExpenseRequest request, Guid expenseId) =>
        new(expenseId, request.Version, request.LastMonth);

    public static SetEntryAmountCommand ToCommand(EntryAmountRequest request, Guid entryId) =>
        new(entryId, request.Version, request.Amount ?? 0m);

    public static SetEntryStatusCommand ToCommand(EntryStatusRequest request, Guid entryId) =>
        new(entryId, request.Version, ParseEntryStatus(request.Status));

    /// <returns>Falso quando a data falta ou não está em AAAA-MM-DD.</returns>
    public static bool TryParseDueDate(string? text, out DateOnly dueDate) =>
        DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out dueDate);

    // ---- Do resultado para a resposta ----

    public static partial ExpenseGroupResponse ToResponse(ExpenseGroupView view);

    public static partial IReadOnlyList<ExpenseGroupResponse> ToResponse(IReadOnlyList<ExpenseGroupView> views);

    public static partial ExpenseResponse ToResponse(ExpenseView view);

    public static partial IReadOnlyList<ExpenseResponse> ToResponse(IReadOnlyList<ExpenseView> views);

    public static partial ExpenseEntryResponse ToResponse(ExpenseEntryView view);

    public static partial MonthResponse ToResponse(MonthView view);

    private static partial MonthClosingResponse ToResponse(MonthClosingView view);

    private static partial UnitChargeResponse ToResponse(UnitChargeView view);

    // ---- Texto da API <-> enum (camelCase). Valor desconhecido vira 0, que o Application recusa com o código do campo ----

    public static ExpenseKind ParseKind(string? text) => text?.Trim().ToLowerInvariant() switch
    {
        "single" => ExpenseKind.Single,
        "fixed" => ExpenseKind.Fixed,
        "variable" => ExpenseKind.Variable,
        "installment" => ExpenseKind.Installment,
        _ => 0,
    };

    /// <summary>Só pendente, pago e cancelado podem ser enviados. Qualquer outro texto vira um valor fora do enum, que o domínio recusa com o código do campo.</summary>
    public static EntryStatus ParseEntryStatus(string? text) => text?.Trim().ToLowerInvariant() switch
    {
        "pending" => EntryStatus.Pending,
        "paid" => EntryStatus.Paid,
        "canceled" => EntryStatus.Canceled,
        _ => (EntryStatus)(-1),
    };

    /// <returns>Falso quando o texto não é um estado conhecido; vazio é o padrão (ativas).</returns>
    public static bool TryParseStatus(string? text, out ExpenseStatusFilter status)
    {
        switch (text?.Trim().ToLowerInvariant())
        {
            case null or "":
            case "active":
                status = ExpenseStatusFilter.Active;
                return true;
            case "ended":
                status = ExpenseStatusFilter.Ended;
                return true;
            case "all":
                status = ExpenseStatusFilter.All;
                return true;
            default:
                status = default;
                return false;
        }
    }

    private static string EntryStatusName(EntryStatus status) => status switch
    {
        EntryStatus.Pending => "pending",
        EntryStatus.Paid => "paid",
        EntryStatus.Overdue => "overdue",
        EntryStatus.Canceled => "canceled",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Situação de lançamento desconhecida."),
    };

    private static string BasisName(ApportionmentBasisKind basis) => basis switch
    {
        ApportionmentBasisKind.IdealFraction => "idealFraction",
        ApportionmentBasisKind.EqualShare => "equalShare",
        ApportionmentBasisKind.Area => "area",
        _ => throw new ArgumentOutOfRangeException(nameof(basis), basis, "Base de rateio desconhecida."),
    };

    private static string DateText(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string KindName(ExpenseKind kind) => kind switch
    {
        ExpenseKind.Single => "single",
        ExpenseKind.Fixed => "fixed",
        ExpenseKind.Variable => "variable",
        ExpenseKind.Installment => "installment",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Tipo de despesa desconhecido."),
    };
}
