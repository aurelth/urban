using FluentAssertions;
using Urban.Finance.Domain.Closing;
using Urban.SharedKernel;

namespace Urban.UnitTests.Finance.Domain;

public class ChargeSplitterTests
{
    [Fact]
    public void Split_EqualWeights_GivesEqualShares()
    {
        Result<IReadOnlyList<decimal>> result = ChargeSplitter.Split(300m, [1m, 1m, 1m]);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Equal(100m, 100m, 100m);
    }

    [Fact]
    public void Split_ProportionalWeights_GivesProportionalShares()
    {
        Result<IReadOnlyList<decimal>> result = ChargeSplitter.Split(1000m, [0.5m, 0.3m, 0.2m]);

        result.Value.Should().Equal(500m, 300m, 200m);
    }

    [Fact]
    public void Split_LeftoverCents_GoToTheLargestFractionalRemainders()
    {
        // 100,00 / 3 = 33,333...: sobra 1 centavo, que vai para a primeira cota (empate no resto, vence o menor índice).
        Result<IReadOnlyList<decimal>> result = ChargeSplitter.Split(100m, [1m, 1m, 1m]);

        result.Value.Should().Equal(33.34m, 33.33m, 33.33m);
        result.Value.Sum().Should().Be(100m);
    }

    [Fact]
    public void Split_TheLargestRemainderWinsTheLeftoverCent()
    {
        // 0,10 nas razões 0,5 / 0,3 / 0,2: cotas exatas de 5 / 3 / 2 centavos.
        ChargeSplitter.Split(0.10m, [0.5m, 0.3m, 0.2m]).Value.Should().Equal(0.05m, 0.03m, 0.02m);

        // 0,05 nas razões 0,4 / 0,4 / 0,2: cotas 2 / 2 / 1 centavos exatos.
        ChargeSplitter.Split(0.05m, [0.4m, 0.4m, 0.2m]).Value.Should().Equal(0.02m, 0.02m, 0.01m);

        // 0,02 nas razões 0,5 / 0,3 / 0,2: pisos 1 / 0 / 0, sobra 1 centavo para o maior resto (0,6 do segundo).
        ChargeSplitter.Split(0.02m, [0.5m, 0.3m, 0.2m]).Value.Should().Equal(0.01m, 0.01m, 0.00m);
    }

    [Fact]
    public void Split_AlwaysSumsExactlyToTheTotal()
    {
        decimal[] weights = [0.0123m, 0.0456m, 0.0789m, 0.1011m, 0.2m, 0.3421m, 0.2m];

        Result<IReadOnlyList<decimal>> result = ChargeSplitter.Split(12345.67m, weights);

        result.Value.Sum().Should().Be(12345.67m);
        result.Value.Should().OnlyContain(share => share >= 0m && decimal.Round(share, 2) == share);
    }

    [Fact]
    public void Split_AZeroTotal_GivesZeroShares()
    {
        ChargeSplitter.Split(0m, [1m, 2m]).Value.Should().Equal(0m, 0m);
    }

    [Fact]
    public void Split_AZeroWeight_GetsNothing()
    {
        ChargeSplitter.Split(100m, [0m, 1m]).Value.Should().Equal(0m, 100m);
    }

    [Fact]
    public void Split_ASingleWeight_GetsTheWholeTotal()
    {
        ChargeSplitter.Split(87.65m, [0.25m]).Value.Should().Equal(87.65m);
    }

    [Fact]
    public void Split_NoWeights_Fails()
    {
        ChargeSplitter.Split(100m, []).Error.Should().Be(ClosingErrors.NoWeights);
    }

    [Fact]
    public void Split_AllWeightsZero_Fails()
    {
        ChargeSplitter.Split(100m, [0m, 0m]).Error.Should().Be(ClosingErrors.NoWeights);
    }

    [Fact]
    public void Split_ANegativeWeight_Fails()
    {
        ChargeSplitter.Split(100m, [1m, -1m]).Error.Should().Be(ClosingErrors.NoWeights);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10.005)]
    [InlineData(10000000)]
    public void Split_AnInvalidTotal_Fails(double total)
    {
        ChargeSplitter.Split((decimal)total, [1m, 1m]).Error.Should().Be(ClosingErrors.TotalInvalid);
    }
}
