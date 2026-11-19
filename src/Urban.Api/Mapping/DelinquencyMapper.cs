using System.Globalization;
using Urban.Api.Endpoints;
using Urban.Finance.Application.Delinquency;

namespace Urban.Api.Mapping;

/// <summary>Do resultado da consulta para a resposta. Datas em AAAA-MM-DD.</summary>
public static class DelinquencyMapper
{
    private const string DateFormat = "yyyy-MM-dd";

    public static DelinquencyResponse ToResponse(DelinquencyView view) => new(
        Format(view.AsOf),
        view.TotalOverdue,
        view.Units,
        view.Page,
        view.PageSize,
        [.. view.Items.Select(item => new DebtorResponse(
            item.UnitId,
            item.Block,
            item.Number,
            item.Balance,
            item.OverdueCharges,
            Format(item.OldestDueDate),
            item.DaysOverdue,
            [.. item.Charges.Select(charge => new OverdueChargeResponse(
                charge.ChargeId, charge.Month, Format(charge.DueDate), charge.Amount, charge.Paid, charge.Balance, charge.DaysOverdue))]))]);

    private static string Format(DateOnly date) => date.ToString(DateFormat, CultureInfo.InvariantCulture);
}
