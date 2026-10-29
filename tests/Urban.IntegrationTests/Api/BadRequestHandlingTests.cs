using System.Net;
using System.Text;
using FluentAssertions;
using Urban.Api.Http;

namespace Urban.IntegrationTests.Api;

public class BadRequestHandlingTests
{
    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task A_body_that_cannot_be_read_is_a_400_with_the_same_code_in_every_environment(string environment)
    {
        await using ApiFactory factory = new() { EnvironmentName = environment };
        using HttpClient client = factory.CreateClient();
        using StringContent broken = new("{ senha: 12345678900 isto nao e json", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.PostAsync("/api/auth/register", broken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        (await response.CodeAsync()).Should().Be("request.invalid");
        (await response.Content.ReadAsStringAsync()).Should().NotContain("12345678900", "o texto recebido nunca volta na resposta");
    }

    [Fact]
    public void The_error_is_a_400_and_has_a_friendly_message()
    {
        RequestErrors.Invalid.Code.Should().Be("request.invalid");
        RequestErrors.Invalid.Description.Should().Be("Não foi possível ler os dados enviados. Confira o formato e tente de novo");
        ErrorHttp.TryGetStatusCode(RequestErrors.Invalid, out int status).Should().BeTrue();
        status.Should().Be(400);
    }
}
