using FluentAssertions;
using Urban.Identity.Domain.Contact;
using Urban.SharedKernel;
using Urban.TestSupport.Identity;

namespace Urban.UnitTests.Identity.Domain;

public class ContactReplyTests
{
    private static readonly Guid MessageId = Guid.CreateVersion7();
    private static readonly Guid AuthorId = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = UserBuilder.Now;

    [Fact]
    public void Create_WithTextOnly_KeepsTheTrimmedTextAndNoAttachments()
    {
        Result<ContactReply> result = ContactReply.Create(MessageId, AuthorId, "  Corrigimos o CNPJ.  ", [], Now);

        result.IsSuccess.Should().BeTrue();
        ContactReply reply = result.Value;
        reply.Id.Should().NotBeEmpty();
        reply.MessageId.Should().Be(MessageId);
        reply.AuthorId.Should().Be(AuthorId);
        reply.Body.Should().Be("Corrigimos o CNPJ.");
        reply.SentAt.Should().Be(Now);
        reply.Attachments.Should().BeEmpty();
    }

    [Fact]
    public void Create_GivesEachReplyItsOwnId()
    {
        Guid first = ContactReply.Create(MessageId, AuthorId, "Primeira resposta.", [], Now).Value.Id;
        Guid second = ContactReply.Create(MessageId, AuthorId, "Segunda resposta.", [], Now).Value.Id;

        first.Should().NotBe(second);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("      ")]
    [InlineData("abcd")]
    [InlineData("  abcd  ")]
    public void Create_RefusesATextThatIsEmptyOrTooShort(string? body)
    {
        ContactReply.Create(MessageId, AuthorId, body, [], Now).Error.Should().Be(ContactReplyErrors.BodyInvalid);
    }

    [Fact]
    public void Create_RefusesATextLongerThanTheLimit()
    {
        string body = new('a', ContactReply.MaxBodyLength + 1);

        ContactReply.Create(MessageId, AuthorId, body, [], Now).Error.Should().Be(ContactReplyErrors.BodyInvalid);
    }

    [Fact]
    public void Create_AcceptsTheLimitsOfTheText()
    {
        ContactReply.Create(MessageId, AuthorId, new string('a', ContactReply.MinBodyLength), [], Now).IsSuccess.Should().BeTrue();
        ContactReply.Create(MessageId, AuthorId, new string('a', ContactReply.MaxBodyLength), [], Now).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Create_KeepsLineBreaksAndTabsButRefusesOtherControlCharacters()
    {
        ContactReply.Create(MessageId, AuthorId, "Linha um.\r\nLinha\tdois.", [], Now).Value.Body.Should().Be("Linha um.\r\nLinha\tdois.");
        ContactReply.Create(MessageId, AuthorId, "Texto com nulo\0 no meio.", [], Now).Error.Should().Be(ContactReplyErrors.BodyInvalid);
    }

    [Fact]
    public void Create_KeepsOnlyTheMetadataOfTheFilesInTheOrderGiven()
    {
        byte[] pdf = SampleFiles.Pdf();
        byte[] png = SampleFiles.Png();

        ContactReply reply = ContactReply.Create(
            MessageId,
            AuthorId,
            "Segue o que você pediu.",
            [new("ata.pdf", pdf), new("foto.png", png)],
            Now).Value;

        reply.Attachments.Should().Equal(
            new ContactReplyAttachment("ata.pdf", "application/pdf", pdf.Length),
            new ContactReplyAttachment("foto.png", "image/png", png.Length));
    }

    [Fact]
    public void Create_AcceptsTheMaximumNumberOfFilesAndRefusesOneMore()
    {
        ContactReplyFile File(int n) => new($"ata-{n}.pdf", SampleFiles.Pdf());

        ContactReply.Create(MessageId, AuthorId, "Três anexos.", [File(1), File(2), File(3)], Now).IsSuccess.Should().BeTrue();
        ContactReply.Create(MessageId, AuthorId, "Quatro anexos.", [File(1), File(2), File(3), File(4)], Now).Error
            .Should().Be(ContactReplyErrors.TooManyAttachments);
    }

    [Fact]
    public void Create_ReportsTheFirstFileThatIsInvalid()
    {
        Result<ContactReply> result = ContactReply.Create(
            MessageId,
            AuthorId,
            "Dois anexos, um ruim.",
            [new("ata.pdf", SampleFiles.Pdf()), new("programa.exe", SampleFiles.Pdf()), new("foto.png", SampleFiles.Pdf())],
            Now);

        result.Error.Should().Be(ContactReplyErrors.AttachmentTypeNotAllowed);
    }

    [Fact]
    public void Create_ChecksTheTextBeforeTheFiles()
    {
        Result<ContactReply> result = ContactReply.Create(MessageId, AuthorId, "x", [new("programa.exe", SampleFiles.Pdf())], Now);

        result.Error.Should().Be(ContactReplyErrors.BodyInvalid);
    }

    [Fact]
    public void Create_AcceptsFilesThatTogetherAddUpToExactlyTheTotalLimit()
    {
        byte[] half = SampleFiles.PaddedTo(SampleFiles.Pdf(), ContactReply.MaxTotalBytes / 2);

        Result<ContactReply> result = ContactReply.Create(MessageId, AuthorId, "Dois arquivos grandes.", [new("a.pdf", half), new("b.pdf", half)], Now);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Create_RefusesFilesThatTogetherPassTheTotalLimitEvenWhenEachOneIsSmallEnough()
    {
        byte[] four = SampleFiles.PaddedTo(SampleFiles.Pdf(), 4 * 1024 * 1024);

        Result<ContactReply> result = ContactReply.Create(
            MessageId,
            AuthorId,
            "Três arquivos grandes.",
            [new("a.pdf", four), new("b.pdf", four), new("c.pdf", four)],
            Now);

        result.Error.Should().Be(ContactReplyErrors.AttachmentsTooLarge);
    }

    [Fact]
    public void Create_RefusesASingleFileOverTheFileLimit()
    {
        byte[] over = SampleFiles.PaddedTo(SampleFiles.Pdf(), ContactReplyAttachment.MaxFileBytes + 1);

        ContactReply.Create(MessageId, AuthorId, "Um arquivo grande demais.", [new("a.pdf", over)], Now).Error
            .Should().Be(ContactReplyErrors.AttachmentTooLarge);
    }

    [Fact]
    public void Limits_AreThreeFilesAndTenMebibytesInTotal()
    {
        ContactReply.MaxAttachments.Should().Be(3);
        ContactReply.MaxTotalBytes.Should().Be(10 * 1024 * 1024);
    }
}
