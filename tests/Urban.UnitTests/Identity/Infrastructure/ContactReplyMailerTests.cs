using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Urban.Identity.Application.Contact;
using Urban.Identity.Domain.Contact;
using Urban.Identity.Infrastructure.Messaging;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.TestSupport.Identity;
using Urban.UnitTests.Support;

namespace Urban.UnitTests.Identity.Infrastructure;

public class ContactReplyMailerTests
{
    private static readonly ContactReplyEmail Reply = new(
        "ana@example.com",
        "Ana Paula",
        "dono@example.com",
        "Corrigir o CNPJ",
        new DateTimeOffset(2026, 11, 13, 12, 0, 0, TimeSpan.Zero),
        "Já corrigimos o CNPJ.\nConfira o documento em anexo.",
        [new ContactReplyFile("comprovante.pdf", SampleFiles.Pdf())]);

    private readonly Mock<IEmailSender> _sender = new();
    private readonly ListLogger<ContactReplyMailer> _logger = new();

    private EmailMessage? _captured;

    [Fact]
    public async Task SendAsync_SendsToTheSenderOfTheMessageWithTheAdminAsReplyToAndAReSubject()
    {
        Capture();

        Result result = await Create().SendAsync(Reply, default);

        result.IsSuccess.Should().BeTrue();
        _captured!.To.Should().Be("ana@example.com");
        _captured.ReplyTo.Should().Be("dono@example.com");
        _captured.Subject.Should().Be("Re: Corrigir o CNPJ");
    }

    [Fact]
    public async Task SendAsync_WritesAGreetingTheTextAndWhichMessageItAnswers()
    {
        Capture();

        await Create().SendAsync(Reply, default);

        _captured!.TextBody.Should().StartWith("Olá Ana Paula,");
        _captured.TextBody.Should().Contain("Já corrigimos o CNPJ.\nConfira o documento em anexo.");
        _captured.TextBody.Should().Contain("Em resposta à sua mensagem \"Corrigir o CNPJ\", enviada em 13/11/2026.");
        _captured.TextBody.Should().Contain("responder diretamente a este e-mail");
        _captured.TextBody.TrimEnd().Should().EndWith("Urban");
    }

    [Fact]
    public async Task SendAsync_WithoutAName_GreetsWithoutOne()
    {
        Capture();

        await Create().SendAsync(Reply with { RecipientName = null }, default);

        _captured!.TextBody.Should().StartWith("Olá,");
    }

    [Fact]
    public async Task SendAsync_ShowsTheDateOfTheMessageInBrasiliaTime()
    {
        Capture();
        DateTimeOffset justAfterMidnightUtc = new(2026, 11, 14, 1, 30, 0, TimeSpan.Zero);

        await Create().SendAsync(Reply with { OriginalSentAt = justAfterMidnightUtc }, default);

        _captured!.TextBody.Should().Contain("enviada em 13/11/2026.");
    }

    [Fact]
    public async Task SendAsync_AttachesTheFilesWithTheirNamesAndContent()
    {
        Capture();
        byte[] png = SampleFiles.Png();

        await Create().SendAsync(Reply with { Files = [new("comprovante.pdf", SampleFiles.Pdf()), new("foto.png", png)] }, default);

        _captured!.Attachments.Should().HaveCount(2);
        _captured.Attachments![0].FileName.Should().Be("comprovante.pdf");
        _captured.Attachments[0].Content.Should().Equal(SampleFiles.Pdf());
        _captured.Attachments[1].FileName.Should().Be("foto.png");
        _captured.Attachments[1].Content.Should().Equal(png);
    }

    [Fact]
    public async Task SendAsync_WithoutFiles_SendsNoAttachments()
    {
        Capture();

        await Create().SendAsync(Reply with { Files = [] }, default);

        _captured!.Attachments.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task SendAsync_WhenTheProviderFails_ReturnsNotDeliveredAndLogsWithoutPersonalDataOrFiles()
    {
        EmailDeliveryException failure = new("O Brevo recusou o envio (HTTP 401, código unauthorized).");
        _sender
            .Setup(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);

        Result result = await Create().SendAsync(Reply, default);

        result.Error.Should().Be(ContactErrors.ReplyNotDelivered);
        LogEntry entry = _logger.Entries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Warning);
        entry.Exception.Should().BeSameAs(failure);
        entry.Message.Should().Contain("example.com");
        entry.Message.Should().NotContain("ana@example.com").And.NotContain("Ana Paula").And.NotContain("CNPJ").And.NotContain("comprovante");
    }

    [Fact]
    public async Task SendAsync_DoesNotHideFailuresThatAreNotDeliveryErrors()
    {
        _sender
            .Setup(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("bug"));

        Func<Task> send = () => Create().SendAsync(Reply, default);

        await send.Should().ThrowAsync<InvalidOperationException>();
        _logger.Entries.Should().BeEmpty();
    }

    private void Capture()
    {
        _sender
            .Setup(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<EmailMessage, CancellationToken>((m, _) => _captured = m)
            .Returns(Task.CompletedTask);
    }

    private ContactReplyMailer Create() => new(_sender.Object, _logger);
}
