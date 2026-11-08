using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Expenses;
using Urban.Finance.Domain.Months;
using Urban.Finance.Infrastructure.Persistence;
using Urban.SharedKernel;
using Urban.SharedKernel.Contracts;

namespace Urban.IntegrationTests.Finance.Persistence;

[Collection(FinancePostgresCollection.Name)]
public class FinanceMonthClosingsTests(FinancePostgresFixture postgres)
{
    private static readonly DateOnly DueDate = new(2026, 12, 10);

    private readonly FinanceStoreKit _kit = new(postgres);

    private FinanceMonthClosings Closings(Guid? tenant = null) => new(postgres.CreateContext(tenant ?? _kit.CondominiumId));

    private MonthClosing NewClosing(YearMonth month, params ApportionmentUnit[] units)
    {
        Expense expense = Expense.CreateFixed(_kit.CondominiumId, Guid.CreateVersion7(), "Zeladoria", null, month, null, 1000m, 10, FinanceStoreKit.Now).Value;
        ExpenseEntry entry = expense.GenerateEntry(month, FinanceStoreKit.Now).Value;
        ApportionmentUnit[] basis = units.Length > 0
            ? units
            : [new ApportionmentUnit(Guid.CreateVersion7(), null, "101", 0.33333333m), new ApportionmentUnit(Guid.CreateVersion7(), "A", "102", 0.66666667m)];

        return MonthClosing.Close(_kit.CondominiumId, month, month.AddMonths(1).FirstDay.AddDays(9), Guid.CreateVersion7(), FinanceStoreKit.Now, [entry], new ApportionmentBasis(ApportionmentBasisKind.IdealFraction, basis)).Value;
    }

    [Fact]
    public async Task Add_ThenFind_ReturnsTheClosingWithItsChargesInTheSameOrder()
    {
        MonthClosing closing = NewClosing(
            FinanceStoreKit.M(2026, 11),
            new ApportionmentUnit(Guid.CreateVersion7(), null, "2", 0.2m),
            new ApportionmentUnit(Guid.CreateVersion7(), "B", "10", 0.3m),
            new ApportionmentUnit(Guid.CreateVersion7(), "A", "1", 0.5m));

        Result added = await Closings().AddAsync(closing, default);
        MonthClosing? found = await Closings().FindAsync(FinanceStoreKit.M(2026, 11), default);

        added.IsSuccess.Should().BeTrue();
        found.Should().NotBeNull();
        found!.Id.Should().Be(closing.Id);
        found.Total.Should().Be(1000m);
        found.DueDate.Should().Be(DueDate);
        found.BasisKind.Should().Be(ApportionmentBasisKind.IdealFraction);
        found.ClosedBy.Should().Be(closing.ClosedBy);
        found.ClosedAt.Should().Be(FinanceStoreKit.Now);
        found.Charges.Select(c => (c.UnitId, c.Position, c.Block, c.Number, c.Weight, c.Amount)).Should().Equal(
            closing.Charges.Select(c => (c.UnitId, c.Position, c.Block, c.Number, c.Weight, c.Amount)));
        found.Charges.Sum(c => c.Amount).Should().Be(1000m);
    }

    [Fact]
    public async Task Add_ASecondClosingOfTheSameMonth_FailsAsAlreadyClosedAndKeepsTheFirst()
    {
        MonthClosing first = NewClosing(FinanceStoreKit.M(2026, 11));
        await Closings().AddAsync(first, default);

        Result second = await Closings().AddAsync(NewClosing(FinanceStoreKit.M(2026, 11)), default);

        second.Error.Should().Be(ClosingErrors.AlreadyClosed);
        (await Closings().FindAsync(FinanceStoreKit.M(2026, 11), default))!.Id.Should().Be(first.Id);
    }

    [Fact]
    public async Task Find_ForAnotherCondominiumOrAnotherMonth_ReturnsNull()
    {
        await Closings().AddAsync(NewClosing(FinanceStoreKit.M(2026, 11)), default);

        (await Closings(Guid.CreateVersion7()).FindAsync(FinanceStoreKit.M(2026, 11), default)).Should().BeNull();
        (await Closings().FindAsync(FinanceStoreKit.M(2026, 10), default)).Should().BeNull();
    }

    [Fact]
    public async Task ClosedMonthsFrom_ListsOnlyTheClosedMonthsFromTheGivenOneOfThisCondominium()
    {
        await Closings().AddAsync(NewClosing(FinanceStoreKit.M(2026, 9)), default);
        await Closings().AddAsync(NewClosing(FinanceStoreKit.M(2026, 11)), default);
        await Closings().AddAsync(NewClosing(FinanceStoreKit.M(2027, 1)), default);

        IReadOnlySet<YearMonth> closed = await Closings().ClosedMonthsFromAsync(FinanceStoreKit.M(2026, 10), default);

        closed.Should().BeEquivalentTo([FinanceStoreKit.M(2026, 11), FinanceStoreKit.M(2027, 1)]);
        (await Closings(Guid.CreateVersion7()).ClosedMonthsFromAsync(FinanceStoreKit.M(2026, 1), default)).Should().BeEmpty();
    }

    [Fact]
    public async Task Remove_DeletesTheClosingAndItsChargesAndAllowsClosingAgain()
    {
        MonthClosing closing = NewClosing(FinanceStoreKit.M(2026, 11));
        await Closings().AddAsync(closing, default);
        FinanceMonthClosings store = Closings();
        MonthClosing loaded = (await store.FindAsync(FinanceStoreKit.M(2026, 11), default))!;

        await store.RemoveAsync(loaded, default);

        (await Closings().FindAsync(FinanceStoreKit.M(2026, 11), default)).Should().BeNull();
        await using FinanceDbContext db = postgres.CreateContext(_kit.CondominiumId);
        (await db.Set<UnitCharge>().CountAsync(charge => charge.ClosingId == closing.Id)).Should().Be(0);
        (await Closings().AddAsync(NewClosing(FinanceStoreKit.M(2026, 11)), default)).IsSuccess.Should().BeTrue();
    }
}
