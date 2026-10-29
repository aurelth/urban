using FluentAssertions;
using Moq;
using Urban.Condominium.Application.PostalCodes;
using Urban.Condominium.Domain.Contacts;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.UnitTests.Condominium.Application;

public class LookupPostalCodeQueryHandlerTests
{
    private static readonly PostalCodeAddress Paulista = new("01310100", "Avenida Paulista", "Bela Vista", "São Paulo", "SP");

    private readonly Mock<IPostalCodeLookup> _lookup = new(MockBehavior.Strict);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("0131010")]
    [InlineData("013101000")]
    [InlineData("0131010a")]
    [InlineData("00000000")]
    [InlineData("../admin")]
    public async Task Handle_AnInvalidCepFailsWithoutCallingAnyProvider(string? postalCode)
    {
        Result<PostalCodeAddress> result = await Handler().Handle(new LookupPostalCodeQuery(postalCode), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PostalCode.Invalid);
        _lookup.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("01310100")]
    [InlineData("01310-100")]
    [InlineData("01.310-100")]
    public async Task Handle_AValidCepReturnsTheAddress_AskingForTheDigitsOnly(string postalCode)
    {
        _lookup.Setup(l => l.LookupAsync("01310100", It.IsAny<CancellationToken>())).ReturnsAsync(PostalCodeLookupResult.Found(Paulista));

        Result<PostalCodeAddress> result = await Handler().Handle(new LookupPostalCodeQuery(postalCode), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(Paulista);
    }

    [Fact]
    public async Task Handle_ACepThatDoesNotExistFailsWithNotFound()
    {
        _lookup.Setup(l => l.LookupAsync("99999999", It.IsAny<CancellationToken>())).ReturnsAsync(PostalCodeLookupResult.NotFound);

        Result<PostalCodeAddress> result = await Handler().Handle(new LookupPostalCodeQuery("99999999"), default);

        result.Error.Should().Be(PostalCodeErrors.NotFound);
        result.Error.Code.Should().Be("condominium.postal_code.not_found");
    }

    [Fact]
    public async Task Handle_AServiceThatIsDownFailsWithLookupUnavailable()
    {
        _lookup.Setup(l => l.LookupAsync("01310100", It.IsAny<CancellationToken>())).ReturnsAsync(PostalCodeLookupResult.Unavailable);

        Result<PostalCodeAddress> result = await Handler().Handle(new LookupPostalCodeQuery("01310100"), default);

        result.Error.Should().Be(PostalCodeErrors.LookupUnavailable);
        result.Error.Code.Should().Be("condominium.postal_code.lookup_unavailable");
    }

    [Fact]
    public void TheErrorsExplainWhatToDoNext()
    {
        PostalCodeErrors.NotFound.Description.Should().Contain("manualmente");
        PostalCodeErrors.LookupUnavailable.Description.Should().Contain("manualmente");
    }

    private LookupPostalCodeQueryHandler Handler() => new(_lookup.Object);
}
