using FluentAssertions;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;

namespace Urban.UnitTests.Identity.Domain;

public class RequestedCondominiumNameTests
{
    [Theory]
    [InlineData("Residencial Vila Verde", "Residencial Vila Verde")]
    [InlineData("  Edifício   Sol  &  Mar  ", "Edifício Sol & Mar")]
    [InlineData("Cond. Parque das Águas nº 2", "Cond. Parque das Águas nº 2")]
    [InlineData("AB", "AB")]
    public void Create_WithAValidName_TrimsAndCollapsesTheSpaces(string input, string expected)
    {
        Result<RequestedCondominiumName> result = RequestedCondominiumName.Create(input);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A")]
    [InlineData("Linha 1\nLinha 2")]
    [InlineData("Com\ttab")]
    public void Create_WithAnInvalidName_Fails(string? input)
    {
        Result<RequestedCondominiumName> result = RequestedCondominiumName.Create(input);

        result.Error.Should().Be(RequestedCondominiumName.Invalid);
    }

    [Fact]
    public void Create_AcceptsTheMaximumLengthAndRefusesOneMore()
    {
        RequestedCondominiumName.Create(new string('a', RequestedCondominiumName.MaxLength)).IsSuccess.Should().BeTrue();
        RequestedCondominiumName.Create(new string('a', RequestedCondominiumName.MaxLength + 1)).Error
            .Should().Be(RequestedCondominiumName.Invalid);
    }

    [Fact]
    public void Invalid_HasAStableCodeAndAPortugueseMessage()
    {
        RequestedCondominiumName.Invalid.Code.Should().Be("identity.requested_condominium_name.invalid");
        RequestedCondominiumName.Invalid.Description.Should().Contain("condomínio");
    }
}
