using FluentAssertions;
using Urban.Finance.Application.Expenses;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Expenses;
using Urban.Finance.Domain.Months;
using Urban.SharedKernel;

namespace Urban.UnitTests.Finance.Application;

public class ListExpensesQueryHandlerTests
{
    private readonly FinanceTestKit _kit = new();
    private readonly ExpenseGroup _group;

    public ListExpensesQueryHandlerTests()
    {
        _group = _kit.NewGroup();
    }

    private static YearMonth M(int year, int month) => FinanceTestKit.M(year, month);

    private ListExpensesQueryHandler Handler() => new(_kit.Expenses, _kit.Tenant, _kit.Clock);

    private async Task<IReadOnlyList<ExpenseView>> ListAsync(ExpenseStatusFilter status)
    {
        Result<IReadOnlyList<ExpenseView>> result = await Handler().Handle(new ListExpensesQuery(status), default);

        return result.Value;
    }

    private void Seed()
    {
        _kit.NewFixed(_group, M(2026, 9), description: "Zeladoria");
        _kit.NewFixed(_group, M(2026, 1), description: "Seguro", end: M(2026, 10));
        _kit.NewInstallment(_group, M(2026, 5), total: 600m, count: 6, current: 4, description: "Obra antiga");
        _kit.NewInstallment(_group, M(2026, 9), total: 600m, count: 6, current: 4, description: "Obra da garagem");
        _kit.NewSingle(_group, M(2026, 6), description: "Troca da lâmpada");
        _kit.NewVariable(_group, M(2026, 1), description: "Energia elétrica");
    }

    [Fact]
    public async Task Handle_Active_HidesWhatAlreadyEnded()
    {
        Seed();

        IReadOnlyList<ExpenseView> active = await ListAsync(ExpenseStatusFilter.Active);

        active.Select(expense => expense.Description).Should().Equal("Energia elétrica", "Obra da garagem", "Zeladoria");
        active.Should().OnlyContain(expense => !expense.IsEnded);
    }

    [Fact]
    public async Task Handle_Ended_ShowsOnlyWhatEndedBeforeTheCurrentMonth()
    {
        Seed();

        IReadOnlyList<ExpenseView> ended = await ListAsync(ExpenseStatusFilter.Ended);

        ended.Select(expense => expense.Description).Should().Equal("Obra antiga", "Seguro", "Troca da lâmpada");
        ended.Should().OnlyContain(expense => expense.IsEnded);
    }

    [Fact]
    public async Task Handle_All_ShowsEverything()
    {
        Seed();

        (await ListAsync(ExpenseStatusFilter.All)).Should().HaveCount(6);
    }

    [Fact]
    public async Task Handle_CurrentAmount_IsTheOneInForceThisMonth()
    {
        Expense expense = _kit.NewFixed(_group, M(2026, 9), 300m);
        expense.ChangeAmountFrom(M(2026, 11), 330m);
        expense.ChangeAmountFrom(M(2027, 3), 400m);

        IReadOnlyList<ExpenseView> list = await ListAsync(ExpenseStatusFilter.All);

        list.Single().CurrentAmount.Should().Be(330m);
    }

    [Fact]
    public async Task Handle_CurrentAmount_OfAnEndedExpense_IsTheLastOne_AndOfAVariableOneIsNull()
    {
        Expense ended = _kit.NewFixed(_group, M(2026, 1), 200m, "Seguro", end: M(2026, 3));
        ended.ChangeAmountFrom(M(2026, 2), 250m);
        _kit.NewVariable(_group, M(2026, 1), "Energia elétrica");

        IReadOnlyList<ExpenseView> list = await ListAsync(ExpenseStatusFilter.All);

        list.Single(expense => expense.Description == "Seguro").CurrentAmount.Should().Be(250m);
        list.Single(expense => expense.Description == "Energia elétrica").CurrentAmount.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ExposesTheVersionAndTheSchedule()
    {
        _kit.NewInstallment(_group, M(2026, 9), total: 600m, count: 6, current: 4);

        ExpenseView view = (await ListAsync(ExpenseStatusFilter.Active)).Single();

        view.Version.Should().Be("5");
        view.LastMonth.Should().Be("2026-11");
        view.Installments.Should().HaveCount(6);
    }

    [Fact]
    public async Task Handle_WithoutATenant_Fails()
    {
        _kit.WithoutATenant();

        (await Handler().Handle(new ListExpensesQuery(), default)).Error.Should().Be(FinanceErrors.CondominiumNotFound);
    }

    [Fact]
    public async Task Handle_ShowsTheDueDayOfEachExpense()
    {
        _kit.NewFixed(_group, M(2026, 9), description: "Zeladoria");

        IReadOnlyList<ExpenseView> active = await ListAsync(ExpenseStatusFilter.Active);

        active.Single().DueDay.Should().Be(10);
    }
}
