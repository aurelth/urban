using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Urban.EmailDelivery;
using Urban.SharedKernel.Abstractions;

namespace Urban.UnitTests.EmailDelivery;

public class BrevoEmailSenderTests
{
    private const string ApiKey = "chave-secreta-de-teste";

    private static readonly EmailMessage Message = new("sindica@example.com", "Confirme seu e-mail", "Abra https://urban-pro.io/confirmar-email?token=segredo");

    [Fact]
    public async Task SendAsync_PostsTheMessageToTheBrevoTransactionalEndpoint()
    {
        StubHandler handler = new(_ => Respond(HttpStatusCode.Created, """{"messageId":"<1@brevo>"}"""));
        BrevoEmailSender sender = Create(handler);

        await sender.SendAsync(Message, default);

        handler.Request!.Method.Should().Be(HttpMethod.Post);
        handler.Request.RequestUri.Should().Be("https://api.brevo.com/v3/smtp/email");
        handler.Request.Headers.GetValues("api-key").Should().ContainSingle().Which.Should().Be(ApiKey);
        handler.Request.Headers.Accept.Should().ContainSingle().Which.MediaType.Should().Be("application/json");
        handler.Request.Content!.Headers.ContentType!.MediaType.Should().Be("application/json");

        using JsonDocument body = JsonDocument.Parse(handler.Body!);
        JsonElement root = body.RootElement;
        root.GetProperty("sender").GetProperty("name").GetString().Should().Be("Urban");
        root.GetProperty("sender").GetProperty("email").GetString().Should().Be("nao-responda@urban-pro.io");
        root.GetProperty("to").GetArrayLength().Should().Be(1);
        root.GetProperty("to")[0].GetProperty("email").GetString().Should().Be("sindica@example.com");
        root.GetProperty("to")[0].TryGetProperty("name", out _).Should().BeFalse();
        root.GetProperty("subject").GetString().Should().Be("Confirme seu e-mail");
        root.GetProperty("textContent").GetString().Should().Be(Message.TextBody);
    }

    [Fact]
    public async Task SendAsync_WithoutReplyToOrAttachments_SendsNeitherField()
    {
        StubHandler handler = new(_ => Respond(HttpStatusCode.Created, """{"messageId":"<1@brevo>"}"""));

        await Create(handler).SendAsync(Message, default);

        using JsonDocument body = JsonDocument.Parse(handler.Body!);
        body.RootElement.TryGetProperty("replyTo", out _).Should().BeFalse();
        body.RootElement.TryGetProperty("attachment", out _).Should().BeFalse();
    }

    [Fact]
    public async Task SendAsync_WithAnEmptyListOfAttachments_SendsNoAttachmentField()
    {
        StubHandler handler = new(_ => Respond(HttpStatusCode.Created, """{"messageId":"<1@brevo>"}"""));

        await Create(handler).SendAsync(Message with { Attachments = [] }, default);

        using JsonDocument body = JsonDocument.Parse(handler.Body!);
        body.RootElement.TryGetProperty("attachment", out _).Should().BeFalse();
    }

    [Fact]
    public async Task SendAsync_WithReplyTo_TellsBrevoWhereAnswersShouldGo()
    {
        StubHandler handler = new(_ => Respond(HttpStatusCode.Created, """{"messageId":"<1@brevo>"}"""));

        await Create(handler).SendAsync(Message with { ReplyTo = "dono@example.com" }, default);

        using JsonDocument body = JsonDocument.Parse(handler.Body!);
        JsonElement replyTo = body.RootElement.GetProperty("replyTo");
        replyTo.GetProperty("email").GetString().Should().Be("dono@example.com");
        replyTo.TryGetProperty("name", out _).Should().BeFalse();
        body.RootElement.GetProperty("sender").GetProperty("email").GetString().Should().Be("nao-responda@urban-pro.io");
    }

    [Fact]
    public async Task SendAsync_WithAttachments_SendsEachOneWithItsNameAndTheContentInBase64()
    {
        byte[] pdf = "%PDF-1.4 conteúdo"u8.ToArray();
        byte[] text = [0x01, 0x02, 0xFF];
        StubHandler handler = new(_ => Respond(HttpStatusCode.Created, """{"messageId":"<1@brevo>"}"""));

        await Create(handler).SendAsync(Message with { Attachments = [new("ata.pdf", pdf), new("dados.bin", text)] }, default);

        using JsonDocument body = JsonDocument.Parse(handler.Body!);
        JsonElement attachments = body.RootElement.GetProperty("attachment");
        attachments.GetArrayLength().Should().Be(2);
        attachments[0].GetProperty("name").GetString().Should().Be("ata.pdf");
        Convert.FromBase64String(attachments[0].GetProperty("content").GetString()!).Should().Equal(pdf);
        attachments[1].GetProperty("name").GetString().Should().Be("dados.bin");
        Convert.FromBase64String(attachments[1].GetProperty("content").GetString()!).Should().Equal(text);
    }

    [Fact]
    public async Task SendAsync_RefusalNeverRepeatsTheNamesOrTheContentOfTheAttachments()
    {
        StubHandler handler = new(_ => Respond(HttpStatusCode.BadRequest, """{"code":"invalid_parameter","message":"attachment ata-secreta.pdf"}"""));
        EmailMessage message = Message with { Attachments = [new("ata-secreta.pdf", [1, 2, 3])] };

        Func<Task> send = () => Create(handler).SendAsync(message, default);

        (await send.Should().ThrowAsync<EmailDeliveryException>()).Which.Message.Should().NotContain("ata-secreta");
    }

    [Fact]
    public async Task SendAsync_RefusalCarriesTheStatusAndTheBrevoCode_NeverTheKeyTheAddressOrTheBody()
    {
        StubHandler handler = new(_ => Respond(HttpStatusCode.Unauthorized, """{"code":"unauthorized","message":"Key not found"}"""));
        BrevoEmailSender sender = Create(handler);

        Func<Task> send = () => sender.SendAsync(Message, default);

        EmailDeliveryException error = (await send.Should().ThrowAsync<EmailDeliveryException>()).Which;
        error.Message.Should().Contain("401").And.Contain("unauthorized");
        error.Message.Should().NotContain(ApiKey).And.NotContain("sindica").And.NotContain("segredo");
    }

    [Theory]
    [InlineData("")]
    [InlineData("<html>erro</html>")]
    [InlineData("[1,2]")]
    [InlineData("""{"message":"sem codigo"}""")]
    [InlineData("""{"code":42}""")]
    public async Task SendAsync_RefusalWithoutAReadableCode_SaysTheCodeIsUnknown(string responseBody)
    {
        StubHandler handler = new(_ => Respond(HttpStatusCode.BadGateway, responseBody));
        BrevoEmailSender sender = Create(handler);

        Func<Task> send = () => sender.SendAsync(Message, default);

        (await send.Should().ThrowAsync<EmailDeliveryException>()).Which.Message.Should().Contain("502").And.Contain("desconhecido");
    }

    [Fact]
    public async Task SendAsync_CapsTheLengthOfTheBrevoCodeItRepeats()
    {
        string code = new('x', 200);
        StubHandler handler = new(_ => Respond(HttpStatusCode.BadRequest, $$"""{"code":"{{code}}"}"""));
        BrevoEmailSender sender = Create(handler);

        Func<Task> send = () => sender.SendAsync(Message, default);

        string message = (await send.Should().ThrowAsync<EmailDeliveryException>()).Which.Message;
        message.Should().Contain(new string('x', 64)).And.NotContain(new string('x', 65));
    }

    [Fact]
    public async Task SendAsync_WrapsNetworkFailures()
    {
        HttpRequestException network = new("dns falhou");
        StubHandler handler = new(_ => throw network);
        BrevoEmailSender sender = Create(handler);

        Func<Task> send = () => sender.SendAsync(Message, default);

        (await send.Should().ThrowAsync<EmailDeliveryException>()).Which.InnerException.Should().BeSameAs(network);
    }

    [Fact]
    public async Task SendAsync_TurnsATimeoutIntoADeliveryError()
    {
        StubHandler handler = new(_ => throw new TaskCanceledException("timeout", new TimeoutException()));
        BrevoEmailSender sender = Create(handler);

        Func<Task> send = () => sender.SendAsync(Message, default);

        (await send.Should().ThrowAsync<EmailDeliveryException>()).Which.Message.Should().Contain("a tempo");
    }

    [Fact]
    public async Task SendAsync_LetsTheCallersCancellationThrough()
    {
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();
        StubHandler handler = new(_ => throw new TaskCanceledException());
        BrevoEmailSender sender = Create(handler);

        Func<Task> send = () => sender.SendAsync(Message, cancellation.Token);

        await send.Should().ThrowAsync<OperationCanceledException>();
    }

    private static BrevoEmailSender Create(StubHandler handler)
    {
        HttpClient http = new(handler) { BaseAddress = new Uri("https://api.brevo.com/") };
        EmailOptions options = new()
        {
            Provider = EmailProvider.Brevo,
            SenderAddress = "nao-responda@urban-pro.io",
            SenderName = "Urban",
            Brevo = new BrevoOptions { ApiKey = ApiKey },
        };

        return new BrevoEmailSender(http, options);
    }

    private static Task<HttpResponseMessage> Respond(HttpStatusCode status, string body) =>
        Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return await respond(request);
        }
    }
}