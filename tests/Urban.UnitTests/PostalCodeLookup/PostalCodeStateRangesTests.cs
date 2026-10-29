using FluentAssertions;
using Urban.PostalCodeLookup;

namespace Urban.UnitTests.PostalCodeLookup;

public class PostalCodeStateRangesTests
{
    // Faixas de CEP por estado, como os Correios as distribuem. Ficam escritas aqui de propósito, sem reaproveitar a tabela do
    // código: se as duas tabelas divergirem em qualquer ponta, algum teste abaixo falha. AM, DF e GO têm duas faixas cada.
    private static readonly (string State, string First, string Last)[] Official =
    [
        ("SP", "01000000", "19999999"),
        ("RJ", "20000000", "28999999"),
        ("ES", "29000000", "29999999"),
        ("MG", "30000000", "39999999"),
        ("BA", "40000000", "48999999"),
        ("SE", "49000000", "49999999"),
        ("PE", "50000000", "56999999"),
        ("AL", "57000000", "57999999"),
        ("PB", "58000000", "58999999"),
        ("RN", "59000000", "59999999"),
        ("CE", "60000000", "63999999"),
        ("PI", "64000000", "64999999"),
        ("MA", "65000000", "65999999"),
        ("PA", "66000000", "68899999"),
        ("AP", "68900000", "68999999"),
        ("AM", "69000000", "69299999"),
        ("RR", "69300000", "69399999"),
        ("AM", "69400000", "69899999"),
        ("AC", "69900000", "69999999"),
        ("DF", "70000000", "72799999"),
        ("GO", "72800000", "72999999"),
        ("DF", "73000000", "73699999"),
        ("GO", "73700000", "76799999"),
        ("RO", "76800000", "76999999"),
        ("TO", "77000000", "77999999"),
        ("MT", "78000000", "78899999"),
        ("MS", "79000000", "79999999"),
        ("PR", "80000000", "87999999"),
        ("SC", "88000000", "89999999"),
        ("RS", "90000000", "99999999"),
    ];

    public static TheoryData<string, string> EdgesInsideTheRange
    {
        get
        {
            TheoryData<string, string> data = [];
            foreach ((string state, string first, string last) in Official)
            {
                data.Add(state, first);
                data.Add(state, last);
            }

            return data;
        }
    }

    public static TheoryData<string, string> NeighboursOutsideTheRange
    {
        get
        {
            TheoryData<string, string> data = [];
            foreach ((string state, string first, string last) in Official)
            {
                int before = int.Parse(first) - 1;
                int after = int.Parse(last) + 1;
                data.Add(state, before.ToString("D8"));
                if (after <= 99999999)
                {
                    data.Add(state, after.ToString("D8"));
                }
            }

            return data;
        }
    }

    [Fact]
    public void TheOfficialTableAboveCoversThe26StatesAndTheFederalDistrict()
    {
        Official.Select(range => range.State).Distinct().Should().HaveCount(27);
    }

    [Theory]
    [MemberData(nameof(EdgesInsideTheRange))]
    public void Matches_AcceptsTheFirstAndTheLastCepOfEachRange(string state, string postalCode)
    {
        PostalCodeStateRanges.Matches(postalCode, state).Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(NeighboursOutsideTheRange))]
    public void Matches_RefusesTheCepRightBeforeAndRightAfterEachRange(string state, string postalCode)
    {
        PostalCodeStateRanges.Matches(postalCode, state).Should().BeFalse();
    }

    [Theory]
    [InlineData("AM", "69100000", true)]
    [InlineData("AM", "69500000", true)]
    [InlineData("AM", "69350000", false)]
    [InlineData("DF", "71000000", true)]
    [InlineData("DF", "73500000", true)]
    [InlineData("DF", "72900000", false)]
    [InlineData("GO", "72900000", true)]
    [InlineData("GO", "75000000", true)]
    [InlineData("GO", "73500000", false)]
    public void Matches_ForStatesWithTwoRanges_AcceptsBothAndRefusesTheGapBetweenThem(string state, string postalCode, bool expected)
    {
        PostalCodeStateRanges.Matches(postalCode, state).Should().Be(expected);
    }

    [Theory]
    [InlineData("99999999", "PR")]
    [InlineData("99999999", "SC")]
    [InlineData("01310100", "RJ")]
    [InlineData("89010000", "PR")]
    [InlineData("00100000", "SP")]
    public void Matches_RefusesAStateThatDoesNotOwnTheCep(string postalCode, string state)
    {
        PostalCodeStateRanges.Matches(postalCode, state).Should().BeFalse();
    }

    [Theory]
    [InlineData("sc")]
    [InlineData("Sc")]
    [InlineData("SC")]
    public void Matches_IgnoresTheCaseOfTheState(string state)
    {
        PostalCodeStateRanges.Matches("89010000", state).Should().BeTrue();
    }

    [Theory]
    [InlineData("XX")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("S")]
    [InlineData("SCC")]
    [InlineData("Santa Catarina")]
    public void Matches_RefusesAnUnknownOrEmptyState(string state)
    {
        PostalCodeStateRanges.Matches("89010000", state).Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("8901000")]
    [InlineData("890100000")]
    [InlineData("8901000a")]
    [InlineData("89010-000")]
    public void Matches_RefusesAnythingButEightDigits(string postalCode)
    {
        Action matches = () => PostalCodeStateRanges.Matches(postalCode, "SC");

        matches.Should().Throw<ArgumentException>();
    }
}
