using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Urban.PostalCodeLookup;
using Urban.SharedKernel.Abstractions;
using Urban.UnitTests.Support;

namespace Urban.UnitTests.PostalCodeLookup;

public class ViaCepPostalCodeLookupTests
{
    private const string Cep = "01310100";

    private const string Found = """
        {"cep":"01310-100","logradouro":"Avenida Paulista","complemento":"de 612 a 1510 - lado par","unidade":"","bairro":"Bela Vista","localidade":"São Paulo","uf":"SP","estado":"São Paulo","regiao":"Sudeste","ibge":"3550308","gia":"1004","ddd":"11","siafi":"7107"}
        """;

    private readonly ListLogger<ViaCepPostalCodeLookup> _logger = new();

    [Fact]
    public async Task LookupAsync_AsksViaCepForTheCepAndReadsTheAddress()
    {
        StubHttpHandler handler = StubHttpHandler.Returning(HttpStatusCode.OK, Found);

        PostalCodeLookupResult result = await Create(handler).LookupAsync(Cep, default);

        result.Should().Be(PostalCodeLookupResult.Found(ScriptedLookup.Paulista));
        handler.Requests.Should().ContainSingle();
        handler.Requests[0].Method.Should().Be(HttpMethod.Get);
        handler.Requests[0].RequestUri.Should().Be("https://viacep.com.br/ws/01310100/json/");
        handler.Requests[0].Headers.Accept.Should().ContainSingle().Which.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task LookupAsync_AnswersWithTheCepThatWasAskedNotTheOneTheProviderSent()
    {
        StubHttpHandler handler = StubHttpHandler.Returning(HttpStatusCode.OK, Found.Replace("01310-100", "99999-999"));

        PostalCodeLookupResult result = await Create(handler).LookupAsync(Cep, default);

        result.Address!.PostalCode.Should().Be(Cep);
    }

    [Fact]
    public async Task LookupAsync_TrimsTheFields()
    {
        StubHttpHandler handler = StubHttpHandler.Returning(
            HttpStatusCode.OK,
            """{"logradouro":" Avenida Paulista ","bairro":" Bela Vista ","localidade":" São Paulo ","uf":" SP "}""");

        PostalCodeLookupResult result = await Create(handler).LookupAsync(Cep, default);

        result.Address.Should().Be(ScriptedLookup.Paulista);
    }

    [Theory]
    [InlineData("""{"localidade":"Rio Branco","uf":"AC","logradouro":"","bairro":""}""")]
    [InlineData("""{"localidade":"Rio Branco","uf":"AC"}""")]
    public async Task LookupAsync_AcceptsACepThatHasNoStreetOrNeighborhood(string body)
    {
        StubHttpHandler handler = StubHttpHandler.Returning(HttpStatusCode.OK, body);

        PostalCodeLookupResult result = await Create(handler).LookupAsync("69900000", default);

        result.Address.Should().Be(new PostalCodeAddress("69900000", string.Empty, string.Empty, "Rio Branco", "AC"));
    }

    [Theory]
    [InlineData("""{"erro":true}""")]
    [InlineData("""{"erro":"true"}""")]
    public async Task LookupAsync_TurnsTheErroFlagIntoNotFound(string body)
    {
        StubHttpHandler handler = StubHttpHandler.Returning(HttpStatusCode.OK, body);

        PostalCodeLookupResult result = await Create(handler).LookupAsync(Cep, default);

        result.Should().Be(PostalCodeLookupResult.NotFound);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task LookupAsync_TurnsAnyRefusalIntoUnavailable(HttpStatusCode status)
    {
        StubHttpHandler handler = StubHttpHandler.Returning(status, string.Empty);

        PostalCodeLookupResult result = await Create(handler).LookupAsync(Cep, default);

        result.Should().Be(PostalCodeLookupResult.Unavailable);
    }

    [Theory]
    [InlineData("")]
    [InlineData("<html>manutenção</html>")]
    [InlineData("[1,2]")]
    [InlineData("""{"uf":"SP"}""")]
    [InlineData("""{"localidade":"São Paulo"}""")]
    [InlineData("""{"localidade":42,"uf":"SP"}""")]
    [InlineData("""{"localidade":" ","uf":"SP"}""")]
    public async Task LookupAsync_TurnsAnAnswerWithoutCityAndStateIntoUnavailable(string body)
    {
        StubHttpHandler handler = StubHttpHandler.Returning(HttpStatusCode.OK, body);

        PostalCodeLookupResult result = await Create(handler).LookupAsync(Cep, default);

        result.Should().Be(PostalCodeLookupResult.Unavailable);
    }

    [Fact]
    public async Task LookupAsync_TurnsANetworkFailureIntoUnavailable()
    {
        StubHttpHandler handler = StubHttpHandler.Throwing(new HttpRequestException("dns falhou"));

        PostalCodeLookupResult result = await Create(handler).LookupAsync(Cep, default);

        result.Should().Be(PostalCodeLookupResult.Unavailable);
    }

    [Fact]
    public async Task LookupAsync_TurnsATimeoutIntoUnavailable()
    {
        StubHttpHandler handler = StubHttpHandler.Throwing(new TaskCanceledException("timeout", new TimeoutException()));

        PostalCodeLookupResult result = await Create(handler).LookupAsync(Cep, default);

        result.Should().Be(PostalCodeLookupResult.Unavailable);
    }

    [Fact]
    public async Task LookupAsync_LetsTheCallersCancellationThrough()
    {
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();
        StubHttpHandler handler = StubHttpHandler.Throwing(new TaskCanceledException());

        Func<Task> lookup = () => Create(handler).LookupAsync(Cep, cancellation.Token);

        await lookup.Should().ThrowAsync<OperationCanceledException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("1234567")]
    [InlineData("123456789")]
    [InlineData("0131010a")]
    [InlineData("01310-100")]
    [InlineData("../admin")]
    public async Task LookupAsync_RefusesAnythingButEightDigitsBeforeCallingTheProvider(string postalCode)
    {
        StubHttpHandler handler = StubHttpHandler.Returning(HttpStatusCode.OK, Found);

        Func<Task> lookup = () => Create(handler).LookupAsync(postalCode, default);

        await lookup.Should().ThrowAsync<ArgumentException>();
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task LookupAsync_LogsWhyTheProviderFailedWithoutTheCep()
    {
        StubHttpHandler handler = StubHttpHandler.Returning(HttpStatusCode.BadGateway, string.Empty);

        await Create(handler).LookupAsync(Cep, default);

        LogEntry entry = _logger.Entries.Should().ContainSingle().Which;
        entry.Level.Should().Be(LogLevel.Warning);
        entry.Message.Should().Contain("ViaCEP").And.Contain("502").And.NotContain(Cep);
    }

    private ViaCepPostalCodeLookup Create(StubHttpHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://viacep.com.br/") }, _logger);
}
