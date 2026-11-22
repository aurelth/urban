using MediatR;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Application.Months;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Months;
using Urban.Finance.Domain.Payments;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;

namespace Urban.Finance.Application.MyMonth;

public sealed class GetMyMonthQueryHandler(
    IExpenseEntries entries, IMonthClosings closings, IExpenseGroups groups, IPayments payments, IResidentHousing housing, ITenantContext tenant, IClock clock)
    : IRequestHandler<GetMyMonthQuery, Result<MyMonthView>>
{
    public async Task<Result<MyMonthView>> Handle(GetMyMonthQuery request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is null)
        {
            return Result.Failure<MyMonthView>(FinanceErrors.CondominiumNotFound);
        }

        Result<YearMonth> month = YearMonth.Parse(request.Month);

        if (month.IsFailure)
        {
            return Result.Failure<MyMonthView>(month.Error);
        }

        DateOnly today = FinanceCalendar.Today(clock.UtcNow);
        ResidentDwelling dwelling = await housing.GetAsync(request.UserId, today, cancellationToken);

        // Sem unidade para cobrar (dependente ou sem vínculo) não há nada financeiro a mostrar: só o aviso de por que a tela está vazia.
        if (dwelling.Units.Count == 0)
        {
            return Result.Success(new MyMonthView(
                month.Value.ToString(), today, MyAccess.Of(dwelling), false, [], null));
        }

        // Uma cobrança por unidade: o mês inteiro cabe numa página só e o que é de cada morador sai dela em memória. A situação sai do
        // ChargeAccount, a mesma regra do painel do síndico e da lista de pagamentos.
        MonthPaymentsPage? page = await payments.ListMonthAsync(month.Value, null, today, 1, int.MaxValue, cancellationToken);

        if (page is null)
        {
            return Result.Success(new MyMonthView(
                month.Value.ToString(), today, MyMonthAccess.Unit, false, [.. dwelling.Units.Select(unit => UnitOf(unit, null))], null));
        }

        Dictionary<Guid, MyChargeView> chargesByUnit = page.Items.ToDictionary(row => row.UnitId, row => ChargeOf(row, page.DueDate, today));
        MonthView expenses = await MonthReader.ReadAsync(entries, closings, groups, month.Value, today, cancellationToken);

        return Result.Success(new MyMonthView(
            month.Value.ToString(),
            today,
            MyMonthAccess.Unit,
            true,
            [.. dwelling.Units.Select(unit => UnitOf(unit, chargesByUnit.GetValueOrDefault(unit.UnitId)))],
            new MyExpensesView(
                expenses.Total,
                [.. expenses.Groups.Where(group => group.Total > 0).Select(group => new MyExpenseGroupView(group.GroupId, group.GroupName, group.Total))])));
    }

    private static MyUnitView UnitOf(ResidentUnit unit, MyChargeView? charge) => new(unit.UnitId, unit.Block, unit.Number, charge);

    private static MyChargeView ChargeOf(ChargeRow row, DateOnly dueDate, DateOnly today) => new(
        row.ChargeId,
        dueDate,
        row.Amount,
        row.Paid,
        ChargeAccount.Balance(row.Amount, row.Paid),
        ChargeAccount.StatusOf(row.Amount, row.Paid, dueDate, today),
        row.LastReceivedOn);
}
