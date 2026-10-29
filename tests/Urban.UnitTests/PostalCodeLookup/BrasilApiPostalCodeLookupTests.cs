using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Urban.PostalCodeLookup;
using Urban.SharedKernel.Abstractions;
using Urban.UnitTests.Support;

namespace Urban.UnitTests.PostalCodeLookup;

public class BrasilApiPostalCodeLookupTests
{
    private const string Cep = "01310100";

    private const string Found = """{"cep":"01310100","state":"SP","city":"São Paulo","neighborhood":"Bela Vista","street":"Avenida Paulista","service":"open-cep"}""";

    private readonly ListLogger<BrasilApiPostalCodeLookup> _logger = new();

    [Fact]
    public async Task LookupAsync_AsksTheBrasilApiForTheCepAndReadsTheAddress()
    {
        StubHttpHandler handler = StubHttpHandler.Returning(HttpStatusCode.OK, Found);

        PostalCodeLookupResult result = await Create(handler).LookupAsync(Cep, default);

        result.Should().Be(PostalCodeLookupResult.Found(ScriptedLookup.Paulista));
        handler.Requests.Should().ContainSingle();
        handler.Requests[0].Method.Should().Be(HttpMethod.Get);
        handler.Requests[0].RequestUri.Should().Be("https://brasilapi.com.br/api/cep/v1/01310100");
        handler.Requests[0].Headers.Accept.Should().ContainSingle().Which.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task LookupAsync_AnswersWithTheCepThatWasAskedNotTheOneTheProviderSent()
    {
        StubHttpHandler handler = StubHttpHandler.Returning(HttpStatusCode.OK, Found.Replace("01310100", "99999999"));

        PostalCodeLookupResult result = await Create(handler).LookupAsync(Cep, default);

        result.Address!.PostalCode.Should().Be(Cep);
    }

    [Fact]
    public async Task LookupAsync_TrimsTheFields()
    {
        StubHttpHandler handler = StubHttpHandler.Returning(
            HttpStatusCode.OK,
            """{"state":" SP ","city":" São Paulo ","neighborhood":" Bela Vista ","street":" Avenida Paulista "}""");

        PostalCodeLookupResult result = await Create(handler).LookupAsync(Cep, default);

        result.Address.Should().Be(ScriptedLookup.Paulista);
    }

    [Theory]
    [InlineData("""{"state":"AC","city":"Rio Branco"}""")]
    [InlineData("""{"state":"AC","city":"Rio Branco","street":null,"neighborhood":null}""")]
    public async Task LookupAsync_AcceptsACepThatHasNoStreetOrNeighborhood(string body)
    {
        StubHttpHandler handler = StubHttpHandler.Returning(HttpStatusCode.OK, body);

        PostalCodeLookupResult result = await Create(handler).LookupAsync("69900000", default);

        result.Address.Should().Be(new PostalCodeAddress("69900000", string.Empty, string.Empty, "Rio Branco", "AC"));
    }

    [Fact]
    public async Task LookupAsync_TurnsA404IntoNotFound()
    {
        StubHttpHandler handler = StubHttpHandler.Returning(HttpStatusCode.NotFound, """{"name":"CepPromiseError","message":"Todos os serviços de CEP retornaram erro."}""");

        PostalCodeLookupResult result = await Create(handler).LookupAsync(Cep, default);

        result.Should().Be(PostalCodeLookupResult.NotFound);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task LookupAsync_TurnsAnyOtherRefusalIntoUnavailable(HttpStatusCode status)
    {
        StubHttpHandler handler = StubHttpHandler.Returning(status, """{"message":"erro"}""");

        PostalCodeLookupResult result = await Create(handler).LookupAsync(Cep, default);

        result.Should().Be(PostalCodeLookupResult.Unavailable);
    }

    [Theory]
    [InlineData("")]
    [InlineData("<html>manutenção</html>")]
    [InlineData("[1,2]")]
    [InlineData("""{"state":"SP"}""")]
    [InlineData("""{"city":"São Paulo"}""")]
    [InlineData("""{"state":"SP","city":42}""")]
    [InlineData("""{"state":" ","city":"São Paulo"}""")]
    [InlineData("""{"state":"SP","city":" "}""")]
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
        entry.Message.Should().Contain("BrasilAPI").And.Contain("502").And.NotContain(Cep);
    }

    private BrasilApiPostalCodeLookup Create(StubHttpHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://brasilapi.com.br/") }, _logger);
}
