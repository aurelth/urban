using System.Net;
using System.Text;

namespace Urban.UnitTests.PostalCodeLookup;

/// <summary>Responde no lugar do provedor de CEP: o teste nunca sai para a rede.</summary>
internal sealed class StubHttpHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];

    public static StubHttpHandler Returning(HttpStatusCode status, string body) => new(_ => Respond(status, body));

    public static StubHttpHandler Throwing(Exception exception) => new(_ => throw exception);

    public static Task<HttpResponseMessage> Respond(HttpStatusCode status, string body) =>
        Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return respond(request);
    }
}
