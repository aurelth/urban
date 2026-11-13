namespace Urban.Identity.Application.Contact;

public interface IContactMessageNotifier
{
    Task NotifyAsync(ContactMessageSummary message, CancellationToken cancellationToken);
}
