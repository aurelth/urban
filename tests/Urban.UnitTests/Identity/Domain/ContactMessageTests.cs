using FluentAssertions;
using Urban.Identity.Domain.Contact;
using Urban.SharedKernel;
using Urban.TestSupport.Identity;

namespace Urban.UnitTests.Identity.Domain;

public class ContactMessageTests
{
    private static readonly Guid SenderId = Guid.CreateVersion7();
    private static readonly Guid CondominiumId = Guid.CreateVersion7();

    [Fact]
    public void Send_WithValidText_CreatesAnUnreadMessageFromTheSender()
    {
        Result<ContactMessage> result = ContactMessage.Send(SenderId, CondominiumId, "Corrigir o CNPJ", "O CNPJ do meu condomínio está errado.", UserBuilder.Now);

        result.IsSuccess.Should().BeTrue();
        ContactMessage message = result.Value;
        message.Id.Should().NotBeEmpty();
        message.SenderId.Should().Be(SenderId);
        message.CondominiumId.Should().Be(CondominiumId);
        message.Subject.Should().Be("Corrigir o CNPJ");
        message.Body.Should().Be("O CNPJ do meu condomínio está errado.");
        message.CreatedAt.Should().Be(UserBuilder.Now);
        message.ReadAt.Should().BeNull();
        message.IsRead.Should().BeFalse();
    }

    [Fact]
    public void Send_WithoutACondominium_StillCreatesTheMessage()
    {
        ContactMessage message = ContactMessage.Send(SenderId, null, "Dúvida sobre o cadastro", "Não sei como cadastrar o condomínio.", UserBuilder.Now).Value;

        message.CondominiumId.Should().BeNull();
    }

    [Fact]
    public void Send_TrimsTheSubjectAndTheBody()
    {
        ContactMessage message = ContactMessage.Send(SenderId, null, "  Dúvida  ", "\n  Preciso de ajuda com o rateio.  \n", UserBuilder.Now).Value;

        message.Subject.Should().Be("Dúvida");
        message.Body.Should().Be("Preciso de ajuda com o rateio.");
    }

    [Fact]
    public void Send_KeepsTheLineBreaksOfTheBody()
    {
        ContactMessage message = ContactMessage.Send(SenderId, null, "Dúvida", "Primeira linha.\r\nSegunda linha.\tcom tabulação", UserBuilder.Now).Value;

        message.Body.Should().Be("Primeira linha.\r\nSegunda linha.\tcom tabulação");
    }

    [Fact]
    public void Send_GivesEachMessageItsOwnId()
    {
        ContactMessage first = ContactMessage.Send(SenderId, null, "Assunto", "Texto da mensagem.", UserBuilder.Now).Value;
        ContactMessage second = ContactMessage.Send(SenderId, null, "Assunto", "Texto da mensagem.", UserBuilder.Now).Value;

        first.Id.Should().NotBe(second.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ab")]
    [InlineData("  ab  ")]
    public void Send_WithASubjectTooShortOrBlank_FailsWithSubjectInvalid(string? subject)
    {
        Result<ContactMessage> result = ContactMessage.Send(SenderId, null, subject, "Texto da mensagem.", UserBuilder.Now);

        result.Error.Should().Be(ContactMessageErrors.SubjectInvalid);
    }

    [Fact]
    public void Send_WithASubjectLongerThanTheLimit_FailsWithSubjectInvalid()
    {
        Result<ContactMessage> result = ContactMessage.Send(SenderId, null, new string('a', ContactMessage.MaxSubjectLength + 1), "Texto da mensagem.", UserBuilder.Now);

        result.Error.Should().Be(ContactMessageErrors.SubjectInvalid);
    }

    [Fact]
    public void Send_WithASubjectOfExactlyTheLimits_Succeeds()
    {
        ContactMessage.Send(SenderId, null, new string('a', ContactMessage.MinSubjectLength), "Texto da mensagem.", UserBuilder.Now).IsSuccess.Should().BeTrue();
        ContactMessage.Send(SenderId, null, new string('a', ContactMessage.MaxSubjectLength), "Texto da mensagem.", UserBuilder.Now).IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData("Duas\nlinhas")]
    [InlineData("Com\ttabulação")]
    [InlineData("Com\u0000nulo")]
    public void Send_WithControlCharactersInTheSubject_FailsWithSubjectInvalid(string subject)
    {
        Result<ContactMessage> result = ContactMessage.Send(SenderId, null, subject, "Texto da mensagem.", UserBuilder.Now);

        result.Error.Should().Be(ContactMessageErrors.SubjectInvalid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n  ")]
    [InlineData("curta")]
    [InlineData("   curta   ")]
    public void Send_WithABodyTooShortOrBlank_FailsWithBodyInvalid(string? body)
    {
        Result<ContactMessage> result = ContactMessage.Send(SenderId, null, "Assunto", body, UserBuilder.Now);

        result.Error.Should().Be(ContactMessageErrors.BodyInvalid);
    }

    [Fact]
    public void Send_WithABodyLongerThanTheLimit_FailsWithBodyInvalid()
    {
        Result<ContactMessage> result = ContactMessage.Send(SenderId, null, "Assunto", new string('a', ContactMessage.MaxBodyLength + 1), UserBuilder.Now);

        result.Error.Should().Be(ContactMessageErrors.BodyInvalid);
    }

    [Fact]
    public void Send_WithABodyOfExactlyTheLimits_Succeeds()
    {
        ContactMessage.Send(SenderId, null, "Assunto", new string('a', ContactMessage.MinBodyLength), UserBuilder.Now).IsSuccess.Should().BeTrue();
        ContactMessage.Send(SenderId, null, "Assunto", new string('a', ContactMessage.MaxBodyLength), UserBuilder.Now).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Send_WithControlCharactersInTheBody_FailsWithBodyInvalid()
    {
        Result<ContactMessage> result = ContactMessage.Send(SenderId, null, "Assunto", "Texto com\u0000nulo no meio", UserBuilder.Now);

        result.Error.Should().Be(ContactMessageErrors.BodyInvalid);
    }

    [Fact]
    public void Send_ChecksTheSubjectBeforeTheBody()
    {
        Result<ContactMessage> result = ContactMessage.Send(SenderId, null, "a", "b", UserBuilder.Now);

        result.Error.Should().Be(ContactMessageErrors.SubjectInvalid);
    }

    [Fact]
    public void MarkRead_OnAnUnreadMessage_RecordsWhenItWasReadAndReportsTheChange()
    {
        ContactMessage message = NewMessage();
        DateTimeOffset readAt = UserBuilder.Now.AddHours(1);

        bool changed = message.MarkRead(readAt);

        changed.Should().BeTrue();
        message.ReadAt.Should().Be(readAt);
        message.IsRead.Should().BeTrue();
    }

    [Fact]
    public void MarkRead_OnAnAlreadyReadMessage_KeepsTheFirstReadingAndReportsNoChange()
    {
        ContactMessage message = NewMessage();
        DateTimeOffset first = UserBuilder.Now.AddHours(1);
        message.MarkRead(first);

        bool changed = message.MarkRead(UserBuilder.Now.AddHours(5));

        changed.Should().BeFalse();
        message.ReadAt.Should().Be(first);
    }

    private static ContactMessage NewMessage() =>
        ContactMessage.Send(SenderId, CondominiumId, "Assunto", "Texto da mensagem.", UserBuilder.Now).Value;
}
