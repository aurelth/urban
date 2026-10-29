using System.Net;
using FluentAssertions;
using Urban.PostalCodeLookup;
using Urban.SharedKernel.Abstractions;
using Urban.UnitTests.Support;

namespace Urban.UnitTests.PostalCodeLookup;

/// <summary>
/// O caso real do CEP 99999-999: a BrasilAPI (fonte secundária) devolveu um endereço de Sarandi, PR, para um CEP que é da faixa
/// do Rio Grande do Sul, e o ViaCEP diz que o CEP não existe. Os dois provedores de verdade, ligados como em produção.
/// </summary>
public class InconsistentProviderAnswerTests
{
    private const string Cep = "99999999";

    private const string BrasilApiSarandi = """{"state":"PR","city":"Sarandi","neighborhood":"Jardim Centro Cívico","street":"Avenida das Torres","service":"open-cep"}""";

    private const string ViaCepSarandi = """{"logradouro":"Avenida das Torres","bairro":"Jardim Centro Cívico","localidade":"Sarandi","uf":"PR"}""";

    private const string ViaCepPassoFundo = """{"logradouro":"Rua Sete de Setembro","bairro":"Centro","localidade":"Passo Fundo","uf":"RS"}""";

    private const string ViaCepNotFound = """{"erro":"true"}""";

    [Fact]
    public async Task LookupAsync_WhenTheBrasilApiAnswersAStateOutsideTheRangeAndViaCepHasTheCep_UsesViaCep()
    {
        PostalCodeLookupResult result = await Chain(BrasilApiSarandi, ViaCepPassoFundo).LookupAsync(Cep, default);

        result.Should().Be(PostalCodeLookupResult.Found(new PostalCodeAddress(Cep, "Rua Sete de Setembro", "Centro", "Passo Fundo", "RS")));
    }

    [Fact]
    public async Task LookupAsync_WhenTheBrasilApiAnswersAStateOutsideTheRangeAndViaCepDoesNotKnowTheCep_AnswersNotFound()
    {
        PostalCodeLookupResult result = await Chain(BrasilApiSarandi, ViaCepNotFound).LookupAsync(Cep, default);

        result.Should().Be(PostalCodeLookupResult.NotFound);
    }

    [Fact]
    public async Task LookupAsync_WhenBothAnswerAStateOutsideTheRange_AnswersNotFound()
    {
        PostalCodeLookupResult result = await Chain(BrasilApiSarandi, ViaCepSarandi).LookupAsync(Cep, default);

        result.Should().Be(PostalCodeLookupResult.NotFound);
    }

    private static FallbackPostalCodeLookup Chain(string brasilApiBody, string viaCepBody) =>
        new(
            new BrasilApiPostalCodeLookup(
                new HttpClient(StubHttpHandler.Returning(HttpStatusCode.OK, brasilApiBody)) { BaseAddress = new Uri("https://brasilapi.com.br/") },
                new ListLogger<BrasilApiPostalCodeLookup>()),
            new ViaCepPostalCodeLookup(
                new HttpClient(StubHttpHandler.Returning(HttpStatusCode.OK, viaCepBody)) { BaseAddress = new Uri("https://viacep.com.br/") },
                new ListLogger<ViaCepPostalCodeLookup>()));
}
