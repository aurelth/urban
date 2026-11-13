using Urban.SharedKernel;

namespace Urban.Identity.Application.Contact;

public interface IContactReplyMailer
{
    /// <returns>Falha com <see cref="ContactErrors.ReplyNotDelivered"/> se o provedor recusar ou não responder.</returns>
    Task<Result> SendAsync(ContactReplyEmail email, CancellationToken cancellationToken);
}
