using MediatR;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.RegistrationRequests;
using Urban.Identity.Domain.Contact;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Identity.Application.Contact.SendContactReply;

/// <summary>
/// Responde a um síndico por e-mail. Tudo é conferido antes do envio, e o envio vem antes da gravação: se o provedor recusar, nada
/// fica registrado e o administrador vê o erro na hora para tentar de novo. Responder também marca a mensagem como lida. O arquivo
/// segue direto para o e-mail e não é guardado; o que vai no e-mail é o nome já limpo, nunca o que veio na requisição.
/// </summary>
public sealed class SendContactReplyCommandHandler(
    IUserAccounts accounts,
    IContactMessages messages,
    IContactReplies replies,
    IContactReplyMailer mailer,
    IClock clock) : IRequestHandler<SendContactReplyCommand, Result<ContactReplyReceipt>>
{
    public async Task<Result<ContactReplyReceipt>> Handle(SendContactReplyCommand request, CancellationToken cancellationToken)
    {
        User? actor = await accounts.FindByIdAsync(request.ActorId, cancellationToken);
        if (!RegistrationReviewers.IsReviewer(actor))
        {
            return Result.Failure<ContactReplyReceipt>(ContactErrors.ReplyForbidden);
        }

        ContactMessage? message = await messages.FindAsync(request.MessageId, cancellationToken);
        if (message is null)
        {
            return Result.Failure<ContactReplyReceipt>(ContactErrors.MessageNotFound);
        }

        User? recipient = await accounts.FindByIdAsync(message.SenderId, cancellationToken);
        if (recipient is null or { Status: AccountStatus.Deleted })
        {
            return Result.Failure<ContactReplyReceipt>(ContactErrors.ReplyRecipientUnavailable);
        }

        DateTimeOffset now = clock.UtcNow;
        Result<ContactReply> created = ContactReply.Create(message.Id, actor!.Id, request.Body, request.Files, now);
        if (!created.IsSuccess)
        {
            return Result.Failure<ContactReplyReceipt>(created.Error);
        }

        ContactReply reply = created.Value;
        ContactReplyFile[] files = [.. reply.Attachments.Select((attachment, index) => new ContactReplyFile(attachment.FileName, request.Files[index].Content))];
        ContactReplyEmail email = new(recipient.Email.Value, recipient.DisplayName?.Value, actor.Email.Value, message.Subject, message.CreatedAt, reply.Body, files);

        Result sent = await mailer.SendAsync(email, cancellationToken);
        if (!sent.IsSuccess)
        {
            return Result.Failure<ContactReplyReceipt>(sent.Error);
        }

        await replies.AddAsync(reply, cancellationToken);
        if (message.MarkRead(now))
        {
            await messages.MarkReadAsync(message, cancellationToken);
        }

        return Result.Success(new ContactReplyReceipt(reply.Id, reply.SentAt, reply.Attachments));
    }
}
