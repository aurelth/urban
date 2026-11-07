using FluentAssertions;
using Urban.Finance.Domain.Expenses;
using Urban.SharedKernel;

namespace Urban.UnitTests.Finance.Domain;

public class InstallmentPlanTests
{
    [Fact]
    public void Split_ATotalThatDividesEvenly_GivesEqualInstallments()
    {
        Result<IReadOnlyList<decimal>> result = InstallmentPlan.Split(600m, 6);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Equal(100m, 100m, 100m, 100m, 100m, 100m);
    }

    [Fact]
    public void Split_ATotalWithLeftoverCents_PutsTheDifferenceOnTheLastInstallment()
    {
        Result<IReadOnlyList<decimal>> result = InstallmentPlan.Split(100m, 3);

        result.Value.Should().Equal(33.33m, 33.33m, 33.34m);
        result.Value.Sum().Should().Be(100m);
    }

    [Fact]
    public void Split_TheSmallestTotalThatFits_GivesOneCentPerInstallmentAndTheRestAtTheEnd()
    {
        Result<IReadOnlyList<decimal>> result = InstallmentPlan.Split(0.03m, 2);

        result.Value.Should().Equal(0.01m, 0.02m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(10.005)]
    [InlineData(10000000)]
    public void Split_AnInvalidTotal_Fails(double total)
    {
        InstallmentPlan.Split((decimal)total, 3).Error.Should().Be(ExpenseErrors.AmountInvalid);
    }

    [Fact]
    public void Split_ATotalTooSmallForOneCentPerInstallment_Fails()
    {
        InstallmentPlan.Split(0.01m, 2).Error.Should().Be(ExpenseErrors.AmountInvalid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(121)]
    [InlineData(-3)]
    public void Split_ACountOutsideTwoToOneHundredAndTwenty_Fails(int count)
    {
        InstallmentPlan.Split(100m, count).Error.Should().Be(ExpenseErrors.InstallmentCountInvalid);
    }

    [Fact]
    public void Split_TheLimitCounts_Work()
    {
        InstallmentPlan.Split(120m, 120).Value.Should().HaveCount(120).And.OnlyContain(amount => amount == 1m);
        InstallmentPlan.Split(100m, 2).Value.Should().Equal(50m, 50m);
    }
}
