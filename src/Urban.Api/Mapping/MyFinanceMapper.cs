using System.Globalization;
using Urban.Api.Endpoints;
using Urban.Finance.Application.MyMonth;

namespace Urban.Api.Mapping;

/// <summary>Do resultado da consulta para a resposta. Datas em AAAA-MM-DD; a situação e o acesso em minúsculas.</summary>
public static class MyFinanceMapper
{
    private const string DateFormat = "yyyy-MM-dd";

    public static MyMonthResponse ToResponse(MyMonthView view) => new(
        view.Month,
        Format(view.AsOf),
        AccessName(view.Access),
        view.Closed,
        [.. view.Units.Select(unit => new MyUnitResponse(unit.UnitId, unit.Block, unit.Number, unit.Charge is null ? null : ToResponse(unit.Charge)))],
        view.Expenses is null
            ? null
            : new MyExpensesResponse(view.Expenses.Total, [.. view.Expenses.Groups.Select(group => new MyExpenseGroupResponse(group.GroupId, group.GroupName, group.Total))]));

    private static MyChargeResponse ToResponse(MyChargeView charge) => new(
        charge.ChargeId,
        Format(charge.DueDate),
        charge.Amount,
        charge.Paid,
        charge.Balance,
        PaymentMapper.StatusText(charge.Status),
        charge.LastReceivedOn is { } received ? Format(received) : null);

    private static string AccessName(MyMonthAccess access) => access switch
    {
        MyMonthAccess.Unit => "unit",
        MyMonthAccess.Dependent => "dependent",
        MyMonthAccess.None => "none",
        _ => throw new ArgumentOutOfRangeException(nameof(access), access, null),
    };

    private static string Format(DateOnly date) => date.ToString(DateFormat, CultureInfo.InvariantCulture);
}
